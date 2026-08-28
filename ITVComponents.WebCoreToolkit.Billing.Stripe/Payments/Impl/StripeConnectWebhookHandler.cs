using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Stripe.Checkout;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Processes the events of connected accounts. Its own endpoint with its own signing secret, because the
    /// provider treats "events on connected accounts" as a separate subscription — sharing the platform endpoint
    /// would mean trying two secrets blindly on every request and never knowing which one was meant.
    /// <para>
    /// Two rules run through everything here. First, an event's account is checked against the account we have
    /// on file: an event for a FOREIGN account must never touch a sale. Second, status only ever moves forward —
    /// delivery is at-least-once and unordered, so a late event must not undo a newer state.
    /// </para>
    /// </summary>
    public class StripeConnectWebhookHandler<TContext> : IStripeConnectWebhookHandler
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IStripeClient client;
        private readonly IGlobalSettings<StripePaymentsOptions> settings;
        private readonly TenantSaleNotifier notifier;

        public StripeConnectWebhookHandler(IDbContextFactory<TContext> dbFactory, IStripeClient client,
            IGlobalSettings<StripePaymentsOptions> settings, IEnumerable<ITenantSaleObserver> observers)
        {
            this.dbFactory = dbFactory;
            this.client = client;
            this.settings = settings;
            notifier = new TenantSaleNotifier(observers);
        }

        /// <inheritdoc />
        public async Task HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
        {
            var options = settings.Value;
            var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, options.ConnectWebhookSecret);
            var accountId = stripeEvent.Account;

            switch (stripeEvent.Type)
            {
                case EventTypes.AccountUpdated:
                    if (stripeEvent.Data.Object is Account account)
                    {
                        await MirrorAccountAsync(account, cancellationToken);
                    }

                    break;
                case EventTypes.AccountApplicationDeauthorized:
                    await MarkDisconnectedAsync(accountId, cancellationToken);
                    break;
                case EventTypes.CheckoutSessionCompleted:
                    if (stripeEvent.Data.Object is Session completed)
                    {
                        await MarkPaidAsync(completed, accountId, cancellationToken);
                    }

                    break;
                case EventTypes.CheckoutSessionExpired:
                    if (stripeEvent.Data.Object is Session expired)
                    {
                        await MoveToAsync(expired.ClientReferenceId, expired.PaymentIntentId, accountId, TenantSaleStatus.Expired, cancellationToken);
                    }

                    break;
                case EventTypes.PaymentIntentPaymentFailed:
                    if (stripeEvent.Data.Object is PaymentIntent failed)
                    {
                        await MoveToAsync(null, failed.Id, accountId, TenantSaleStatus.Failed, cancellationToken);
                    }

                    break;
                case EventTypes.ChargeRefunded:
                    if (stripeEvent.Data.Object is Charge charge)
                    {
                        await MirrorRefundsAsync(charge, accountId, cancellationToken);
                    }

                    break;
            }
        }

        /// <summary>Keeps the local account mirror in step — an account that works today can be restricted tomorrow.</summary>
        private async Task MirrorAccountAsync(Account remote, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var local = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.ProviderAccountId == remote.Id, cancellationToken);
            if (local == null)
            {
                // An account we never created (or one left over from the other provider mode). Nothing to
                // mirror, but worth seeing: it usually means test and live keys were swapped.
                LogEnvironment.LogEvent(
                    $"Connect event for the unknown account {remote.Id} — no local payout account matches it. Ignored.",
                    LogSeverity.Warning, "StripeConnect");
                return;
            }

            TenantPaymentAccountService<TContext>.Apply(local, remote);
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// The tenant (or the provider) cut the link. The account keeps existing at the provider but is out of
        /// our reach, so selling stops immediately rather than failing one payment at a time.
        /// </summary>
        private async Task MarkDisconnectedAsync(string? accountId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var local = await db.TenantPaymentAccounts.FirstOrDefaultAsync(a => a.ProviderAccountId == accountId, cancellationToken);
            if (local == null)
            {
                return;
            }

            local.Disconnected = true;
            local.ChargesEnabled = false;
            local.PayoutsEnabled = false;
            local.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            LogEnvironment.LogEvent(
                $"The connected account {accountId} of tenant {local.TenantId} was deauthorized; further sales are refused until a new payout account is set up.",
                LogSeverity.Warning, "StripeConnect");
        }

        /// <summary>Pending -&gt; Paid, and the ONE place the completion observers are called.</summary>
        private async Task MarkPaidAsync(Session session, string? accountId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await ResolveSaleAsync(db, session.ClientReferenceId, session.PaymentIntentId, session.Id, accountId, cancellationToken);
            if (sale == null)
            {
                return;
            }

            // The payload may be an older snapshot; the current session decides.
            var current = session;
            try
            {
                current = await new SessionService(client).GetAsync(session.Id,
                    cancellationToken: cancellationToken,
                    requestOptions: PaymentsRuntime.ForAccount(accountId ?? sale.ProviderAccountId!));
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not re-read the checkout session {session.Id} of sale {sale.TenantSaleId}; the event payload is used instead: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
            }

            if (!string.Equals(current.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(current.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase))
            {
                // A completed session whose payment is still processing (delayed payment methods). It arrives
                // again as async_payment_succeeded; nothing is released on a maybe.
                LogEnvironment.LogEvent(
                    $"Checkout session {current.Id} of sale {sale.TenantSaleId} completed with payment status '{current.PaymentStatus}'; the sale stays pending until the payment settles.",
                    LogSeverity.Report, "StripeConnect");
                return;
            }

            sale.ProviderPaymentIntentId ??= current.PaymentIntentId;
            sale.ProviderChargeId ??= await ResolveChargeAsync(current.PaymentIntentId, accountId ?? sale.ProviderAccountId, cancellationToken);

            if (sale.Status != TenantSaleStatus.Pending)
            {
                // Delivered more than once. The identifiers above may still be new, so they are saved — but the
                // order must not be released a second time.
                sale.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            sale.Status = TenantSaleStatus.Paid;
            sale.PaidUtc = DateTime.UtcNow;
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            await notifier.NotifyCompletedAsync(sale, cancellationToken);
        }

        /// <summary>Moves an unpaid sale to a terminal state. Never touches one that has already been paid.</summary>
        private async Task MoveToAsync(string? clientReferenceId, string? paymentIntentId, string? accountId, TenantSaleStatus status, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await ResolveSaleAsync(db, clientReferenceId, paymentIntentId, null, accountId, cancellationToken);
            if (sale is not { Status: TenantSaleStatus.Pending })
            {
                return;
            }

            sale.Status = status;
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Mirrors refunds the provider knows about but we do not — a refund issued straight from the provider
        /// dashboard arrives only this way. Refunds we booked ourselves are already on file and are skipped,
        /// which is also what keeps the observers from firing twice.
        /// </summary>
        private async Task MirrorRefundsAsync(Charge charge, string? accountId, CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.ProviderChargeId == charge.Id, cancellationToken);
            sale ??= await ResolveSaleAsync(db, null, charge.PaymentIntentId, null, accountId, cancellationToken);
            if (sale == null)
            {
                return;
            }

            if (!BelongsToAccount(sale, accountId))
            {
                return;
            }

            sale.ProviderChargeId ??= charge.Id;
            var known = new HashSet<string>(sale.Refunds.Where(r => r.ProviderRefundId != null).Select(r => r.ProviderRefundId!), StringComparer.Ordinal);
            var fresh = (charge.Refunds?.Data ?? new List<Refund>()).Where(r => !known.Contains(r.Id)).ToList();
            if (fresh.Count == 0)
            {
                sale.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            // How much of the commission the provider actually gave back. Read from the platform's application
            // fee rather than guessed: whether a dashboard refund returned the fee is not visible on the refund.
            var feeRefundedTotal = await ReadRefundedApplicationFeeAsync(charge, sale, cancellationToken);
            var feeAlreadyRecorded = sale.Refunds.Sum(r => r.ApplicationFeeRefundedMinor);
            var feeToDistribute = Math.Max(0, feeRefundedTotal - feeAlreadyRecorded);

            var added = new List<TenantSaleRefund>();
            foreach (var refund in fresh.OrderBy(r => r.Created))
            {
                var share = fresh.Count == 1
                    ? feeToDistribute
                    : Math.Min(feeToDistribute, ApplicationFeeMath.ProportionalRefund(sale.ApplicationFeeMinor, refund.Amount, sale.AmountMinor));
                feeToDistribute -= share;

                var row = new TenantSaleRefund
                {
                    TenantSaleId = sale.TenantSaleId,
                    AmountMinor = refund.Amount,
                    ApplicationFeeRefundedMinor = share,
                    ProviderRefundId = refund.Id,
                    Reason = refund.Reason,
                    Status = refund.Status,
                    Created = refund.Created
                };
                sale.Refunds.Add(row);
                added.Add(row);
            }

            sale.Status = TenantSaleService<TContext>.DeriveStatus(sale.AmountMinor, sale.Refunds.Sum(r => r.AmountMinor));
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            foreach (var row in added)
            {
                await notifier.NotifyRefundedAsync(sale, row, cancellationToken);
            }
        }

        /// <summary>
        /// Total commission returned for this charge, in minor units. The application fee lives on the PLATFORM
        /// account (no account header), which is why this is a separate lookup and not a field on the charge.
        /// </summary>
        private async Task<long> ReadRefundedApplicationFeeAsync(Charge charge, TenantSale sale, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(charge.ApplicationFeeId) || sale.ApplicationFeeMinor <= 0)
            {
                return 0;
            }

            try
            {
                var fee = await new ApplicationFeeService(client).GetAsync(charge.ApplicationFeeId, cancellationToken: cancellationToken);
                return Math.Clamp(fee.AmountRefunded, 0, sale.ApplicationFeeMinor);
            }
            catch (StripeException ex)
            {
                // Falling back to the provider's documented proportional rule. Logged, because a wrong number
                // here shows up as a commission statement nobody can reconcile.
                LogEnvironment.LogEvent(
                    $"Could not read the application fee {charge.ApplicationFeeId} for sale {sale.TenantSaleId}; the returned commission is estimated proportionally: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return ApplicationFeeMath.ProportionalRefund(sale.ApplicationFeeMinor, charge.AmountRefunded, sale.AmountMinor);
            }
        }

        /// <summary>Charge behind a payment intent — the identifier a refund is issued against.</summary>
        private async Task<string?> ResolveChargeAsync(string? paymentIntentId, string? accountId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(paymentIntentId) || string.IsNullOrEmpty(accountId))
            {
                return null;
            }

            try
            {
                var intent = await new PaymentIntentService(client).GetAsync(paymentIntentId,
                    cancellationToken: cancellationToken,
                    requestOptions: PaymentsRuntime.ForAccount(accountId));
                return intent.LatestChargeId;
            }
            catch (StripeException ex)
            {
                // Not fatal for the payment, but it costs the ability to refund from the interface later.
                LogEnvironment.LogEvent(
                    $"Could not read the charge of payment intent {paymentIntentId} on account {accountId}; the sale is paid but has no charge on record: {ex.OutlineException()}",
                    LogSeverity.Warning, "StripeConnect");
                return null;
            }
        }

        /// <summary>
        /// Finds the sale an event belongs to: by the client reference we stamped on the session first, then by
        /// the provider identifiers. The account is verified in every case.
        /// </summary>
        private async Task<TenantSale?> ResolveSaleAsync(TContext db, string? clientReferenceId, string? paymentIntentId, string? sessionId, string? accountId, CancellationToken cancellationToken)
        {
            TenantSale? sale = null;
            if (int.TryParse(clientReferenceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var saleId))
            {
                sale = await db.TenantSales.Include(s => s.Refunds).FirstOrDefaultAsync(s => s.TenantSaleId == saleId, cancellationToken);
            }

            if (sale == null && !string.IsNullOrEmpty(paymentIntentId))
            {
                sale = await db.TenantSales.Include(s => s.Refunds).FirstOrDefaultAsync(s => s.ProviderPaymentIntentId == paymentIntentId, cancellationToken);
            }

            if (sale == null && !string.IsNullOrEmpty(sessionId))
            {
                sale = await db.TenantSales.Include(s => s.Refunds).FirstOrDefaultAsync(s => s.ProviderSessionId == sessionId, cancellationToken);
            }

            if (sale == null)
            {
                return null;
            }

            return BelongsToAccount(sale, accountId) ? sale : null;
        }

        /// <summary>
        /// Guards against an event of one connected account changing another tenant's sale. A mismatch is not a
        /// routine skip — it means either a misconfigured endpoint or a genuinely wrong attribution.
        /// </summary>
        private static bool BelongsToAccount(TenantSale sale, string? accountId)
        {
            if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(sale.ProviderAccountId)
                || string.Equals(sale.ProviderAccountId, accountId, StringComparison.Ordinal))
            {
                return true;
            }

            LogEnvironment.LogEvent(
                $"Connect event for account {accountId} matched sale {sale.TenantSaleId} of tenant {sale.TenantId}, which belongs to account {sale.ProviderAccountId}. The sale is NOT touched.",
                LogSeverity.Error, "StripeConnect");
            return false;
        }
    }
}
