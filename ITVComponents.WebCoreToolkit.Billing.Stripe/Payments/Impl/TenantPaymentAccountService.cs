using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Connect onboarding against Stripe: creates the connected account, hands out the hosted onboarding link and
    /// keeps the local mirror in step.
    /// <para>
    /// Uses a fresh context per operation (<see cref="IDbContextFactory{TContext}"/>) rather than the ambient
    /// scoped one — these calls also happen from a Blazor circuit, where a circuit-wide context and an
    /// <c>await</c> on a provider round-trip is exactly the recipe for a concurrent-use exception.
    /// </para>
    /// </summary>
    public class TenantPaymentAccountService<TContext> : ITenantPaymentAccountService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IStripeClient client;
        private readonly PaymentsRuntime runtime;

        public TenantPaymentAccountService(IDbContextFactory<TContext> dbFactory, IStripeClient client,
            IGlobalSettings<StripePaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates)
        {
            this.dbFactory = dbFactory;
            this.client = client;
            // Resolved as a collection so a missing gate is an empty set instead of a container failure — the
            // runtime then answers "not entitled", which is the safe direction.
            runtime = new PaymentsRuntime(settings, featureGates.FirstOrDefault());
        }

        /// <inheritdoc />
        public async Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);
            return account == null ? null : ToStatus(account);
        }

        /// <inheritdoc />
        public async Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl, string? email = null, string? country = null, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();
            await runtime.EnsureFeatureAsync(tenantId, cancellationToken);

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken);

            if (account is { Disconnected: true })
            {
                // The tenant revoked the link between platform and account. The old acct_ is unreachable for us
                // from now on, so the only way back is a NEW account. Recorded loudly: sales made on the old one
                // keep pointing at it, and someone reconciling later needs to see why two accounts exist.
                LogEnvironment.LogEvent(
                    $"Tenant {tenantId} restarts payout onboarding after the connected account {account.ProviderAccountId} was deauthorized. A new connected account is created; the old one stays referenced by its past sales.",
                    LogSeverity.Warning, "StripeConnect");
                db.TenantPaymentAccounts.Remove(account);
                await db.SaveChangesAsync(cancellationToken);
                account = null;
            }

            if (account == null)
            {
                account = await CreateAccountAsync(db, tenantId, email, country, cancellationToken);
            }

            try
            {
                var link = await new AccountLinkService(client).CreateAsync(new AccountLinkCreateOptions
                {
                    Account = account.ProviderAccountId,
                    Type = "account_onboarding",
                    ReturnUrl = returnUrl,
                    RefreshUrl = refreshUrl
                }, cancellationToken: cancellationToken);
                return link.Url;
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create an onboarding link for tenant {tenantId} (account {account.ProviderAccountId}): {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <inheritdoc />
        public async Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken)
                          ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount, $"Tenant {tenantId} has no payout account.");

            try
            {
                var remote = await new AccountService(client).GetAsync(account.ProviderAccountId, cancellationToken: cancellationToken);
                Apply(account, remote);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (StripeException ex)
            {
                // The mirror is stale, not wrong — show what we have rather than an error page, but leave a
                // trace: a refresh that silently returns yesterday's state is how "it said it was fine" starts.
                LogEnvironment.LogEvent(
                    $"Could not refresh the connected account {account.ProviderAccountId} of tenant {tenantId}; the local mirror is shown unchanged: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
            }

            return ToStatus(account);
        }

        /// <inheritdoc />
        public async Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.AsNoTracking()
                              .FirstOrDefaultAsync(a => a.TenantId == tenantId, cancellationToken)
                          ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount, $"Tenant {tenantId} has no payout account.");

            if (!string.Equals(account.AccountType, "express", StringComparison.OrdinalIgnoreCase))
            {
                // Standard accounts own their provider relationship and log in themselves; a login link would be
                // refused. Null is the answer, not an error.
                return null;
            }

            try
            {
                var link = await new AccountLoginLinkService(client).CreateAsync(account.ProviderAccountId, cancellationToken: cancellationToken);
                return link.Url;
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a dashboard link for the connected account {account.ProviderAccountId} of tenant {tenantId}: {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }
        }

        /// <summary>
        /// Creates the connected account and persists it BEFORE any link is made. The order matters: if the link
        /// call fails, or the tenant simply closes the tab, an unsaved acct_ would be an orphan at the provider
        /// that nothing here can ever find again.
        /// </summary>
        private async Task<TenantPaymentAccount> CreateAccountAsync(TContext db, int tenantId, string? email, string? country, CancellationToken cancellationToken)
        {
            var options = runtime.Options;
            var accountType = string.IsNullOrWhiteSpace(options.AccountType) ? "express" : options.AccountType.Trim().ToLowerInvariant();
            var accountCountry = (string.IsNullOrWhiteSpace(country) ? options.DefaultCountry : country)?.Trim().ToUpperInvariant();

            Account created;
            try
            {
                created = await new AccountService(client).CreateAsync(new AccountCreateOptions
                {
                    Type = accountType,
                    Country = accountCountry,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Capabilities = new AccountCapabilitiesOptions
                    {
                        CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                        Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
                    },
                    Metadata = new Dictionary<string, string> { ["tenantId"] = tenantId.ToString() }
                }, cancellationToken: cancellationToken);
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not create a connected account for tenant {tenantId} (type '{accountType}', country '{accountCountry}'): {ex.OutlineException()}",
                    LogSeverity.Error, "StripeConnect");
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
            }

            var account = new TenantPaymentAccount
            {
                TenantId = tenantId,
                ProviderAccountId = created.Id,
                AccountType = accountType,
                Created = DateTime.UtcNow
            };
            Apply(account, created);
            db.TenantPaymentAccounts.Add(account);
            await db.SaveChangesAsync(cancellationToken);
            return account;
        }

        /// <summary>Copies the provider's view of the account onto the local mirror.</summary>
        internal static void Apply(TenantPaymentAccount account, Account remote)
        {
            account.ChargesEnabled = remote.ChargesEnabled;
            account.PayoutsEnabled = remote.PayoutsEnabled;
            account.DetailsSubmitted = remote.DetailsSubmitted;
            if (!string.IsNullOrEmpty(remote.Country))
            {
                account.Country = remote.Country.ToUpperInvariant();
            }

            if (!string.IsNullOrEmpty(remote.DefaultCurrency))
            {
                account.DefaultCurrency = remote.DefaultCurrency.ToUpperInvariant();
            }

            if (!string.IsNullOrEmpty(remote.Type))
            {
                account.AccountType = remote.Type;
            }

            account.DisabledReason = remote.Requirements?.DisabledReason;
            account.RequirementsJson = SerializeRequirements(remote.Requirements);
            account.Updated = DateTime.UtcNow;
        }

        /// <summary>
        /// Mirrors the requirement lists as raw JSON. Deliberately not modelled: the shape belongs to the
        /// provider and changes without notice, and everything we do with it is show it.
        /// </summary>
        private static string? SerializeRequirements(AccountRequirements? requirements)
        {
            if (requirements == null)
            {
                return null;
            }

            return JsonSerializer.Serialize(new
            {
                currentlyDue = requirements.CurrentlyDue ?? new List<string>(),
                pastDue = requirements.PastDue ?? new List<string>(),
                eventuallyDue = requirements.EventuallyDue ?? new List<string>(),
                pendingVerification = requirements.PendingVerification ?? new List<string>(),
                currentDeadline = requirements.CurrentDeadline,
                disabledReason = requirements.DisabledReason
            });
        }

        private TenantPaymentAccountStatus ToStatus(TenantPaymentAccount account)
        {
            var requirements = DeserializeRequirements(account.RequirementsJson);
            return new TenantPaymentAccountStatus
            {
                TenantId = account.TenantId,
                ProviderAccountId = account.ProviderAccountId,
                AccountType = account.AccountType,
                Country = account.Country,
                DefaultCurrency = account.DefaultCurrency,
                ChargesEnabled = account.ChargesEnabled,
                PayoutsEnabled = account.PayoutsEnabled,
                DetailsSubmitted = account.DetailsSubmitted,
                Disconnected = account.Disconnected,
                DisabledReason = account.DisabledReason,
                CurrentlyDue = requirements.CurrentlyDue,
                PastDue = requirements.PastDue,
                PendingVerification = requirements.PendingVerification,
                CurrentDeadline = requirements.CurrentDeadline,
                CanSell = runtime.CanSell(account),
                Updated = account.Updated
            };
        }

        private static MirroredRequirements DeserializeRequirements(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new MirroredRequirements();
            }

            try
            {
                return JsonSerializer.Deserialize<MirroredRequirements>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new MirroredRequirements();
            }
            catch (JsonException ex)
            {
                // The mirror was written by an older shape of the provider payload. Showing no requirements is
                // survivable; silently pretending there are none is not, hence the log line.
                LogEnvironment.LogEvent(
                    $"Could not read the mirrored connect requirements; the outstanding items are not shown: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return new MirroredRequirements();
            }
        }

        /// <summary>Local shape of the mirrored requirement JSON.</summary>
        private sealed class MirroredRequirements
        {
            public List<string> CurrentlyDue { get; set; } = new();

            public List<string> PastDue { get; set; } = new();

            public List<string> PendingVerification { get; set; } = new();

            public DateTime? CurrentDeadline { get; set; }
        }
    }
}
