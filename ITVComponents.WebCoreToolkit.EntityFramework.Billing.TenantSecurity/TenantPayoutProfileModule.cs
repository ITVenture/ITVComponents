using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity.Resources;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity
{
    /// <summary>
    /// The payout tab of the billing profile: what the payment provider needs to know about a tenant before a
    /// connected account can be created for it.
    /// <para>
    /// Deliberately NOT part of the tenant creation. Signing up and being able to take card payments are two
    /// different days: most tenants never switch payments on, and asking every one of them for a merchant
    /// category code while they are still typing their address would make the sign-up worse for everyone to serve
    /// the few. <see cref="AppliesTo"/> therefore answers yes only while the profile is being EDITED - during
    /// creation the module is not even offered a tab.
    /// </para>
    /// <para>
    /// The tenant's name, e-mail, phone, address and VAT number are NOT asked for again: they are on the billing
    /// profile this tab sits in, and the account service reads them from there. Only what the provider needs on
    /// top of that lives here.
    /// </para>
    /// <para>
    /// The texts come from <c>Resources/PayoutProfileMessages</c> (en neutral, de, fr, it) and are handed out
    /// already translated. The JSON convention of the contract is meant for texts that live in configuration;
    /// code in an assembly uses ordinary resources and gets their fallback chain with them.
    /// </para>
    /// </summary>
    /// <typeparam name="TContext">the host context carrying the payments tables</typeparam>
    public class TenantPayoutProfileModule<TContext> : ICustomCompanyInformationHandler
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>The permission a tenant admin needs to see and change the payout data.</summary>
        public const string PayoutPermission = "Payments.Profile.Write";

        private readonly TContext db;

        /// <summary>
        /// Takes the context itself, not a factory: modules are loaded through the FRESH plugin path, which hands
        /// out a new instance in its own load scope for every single operation and disposes both afterwards. A
        /// context held here therefore lives exactly as long as one call - which is what a factory would have
        /// arranged by hand. It is also the shape a host can actually declare, the scope-owned context being an
        /// ordinary named plugin dependency.
        /// </summary>
        public TenantPayoutProfileModule(TContext db)
        {
            this.db = db;
        }

        /// <summary>Gets or sets the UniqueName of this Plugin</summary>
        public string UniqueName { get; set; } = string.Empty;

        /// <inheritdoc />
        public string Key => "billing.payoutprofile";

        /// <inheritdoc />
        public string Title => PayoutProfileMessages.Text("Payout_Title");

        /// <inheritdoc />
        public string Icon => "Icons.Material.Filled.AccountBalance";

        /// <summary>Empty: the generic form built from <see cref="GetFields"/> is enough for six plain entries.</summary>
        public string ViewKey => string.Empty;

        /// <inheritdoc />
        public string EditPermission => PayoutPermission;

        /// <summary>
        /// Only while EDITING an existing profile. During tenant creation there is no tenant to attach the data
        /// to, no permission to check it against - and no reason to ask: payments are switched on later, if at
        /// all. This one line is what keeps the sign-up short.
        /// </summary>
        public bool AppliesTo(CustomInfoContext ctx) => ctx.Mode == CustomInfoMode.Edit;

        /// <inheritdoc />
        public IReadOnlyList<CustomInfoField> GetFields(CustomInfoContext ctx)
        {
            return new List<CustomInfoField>
            {
                new()
                {
                    Name = nameof(TenantPaymentProfile.Country),
                    Label = PayoutProfileMessages.Text("Payout_Country_Label"),
                    HelpText = PayoutProfileMessages.Text("Payout_Country_Help")
                },
                new()
                {
                    Name = nameof(TenantPaymentProfile.EntityType),
                    Kind = CustomInfoFieldKind.Choice,
                    Label = PayoutProfileMessages.Text("Payout_EntityType_Label"),
                    HelpText = PayoutProfileMessages.Text("Payout_EntityType_Help"),
                    Choices = new List<CustomInfoChoice>
                    {
                        new() { Value = "individual", Label = PayoutProfileMessages.Text("Payout_EntityType_Individual") },
                        new() { Value = "company", Label = PayoutProfileMessages.Text("Payout_EntityType_Company") }
                    }
                },
                new()
                {
                    Name = nameof(TenantPaymentProfile.ContactEmail),
                    Label = PayoutProfileMessages.Text("Payout_ContactEmail_Label"),
                    HelpText = PayoutProfileMessages.Text("Payout_ContactEmail_Help")
                },
                new()
                {
                    Name = nameof(TenantPaymentProfile.DisplayName),
                    Label = PayoutProfileMessages.Text("Payout_DisplayName_Label"),
                    HelpText = PayoutProfileMessages.Text("Payout_DisplayName_Help")
                },
                new()
                {
                    Name = nameof(TenantPaymentProfile.BusinessUrl),
                    Label = PayoutProfileMessages.Text("Payout_BusinessUrl_Label"),
                    HelpText = PayoutProfileMessages.Text("Payout_BusinessUrl_Help")
                }
            };
        }

        /// <summary>
        /// Checks what can be checked without asking the provider. Nothing here is REQUIRED: the tab is reachable
        /// long before anyone wants to take payments, and forcing a merchant category code on a tenant who only
        /// came to fix their address would be absurd. What the account creation genuinely cannot do without is
        /// refused there, by name, at the moment it matters.
        /// </summary>
        public Task<CustomInfoValidation> ValidateAsync(IReadOnlyDictionary<string, string> values,
            JsonNode payload, CustomInfoContext ctx, CancellationToken ct)
        {
            var country = Value(values, nameof(TenantPaymentProfile.Country));
            if (country.Length is not (0 or 2))
            {
                return Task.FromResult(CustomInfoValidation.Failed(
                    PayoutProfileMessages.Text("Payout_Country_Invalid"),
                    nameof(TenantPaymentProfile.Country)));
            }

            var url = Value(values, nameof(TenantPaymentProfile.BusinessUrl));
            if (url.Length != 0 && !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return Task.FromResult(CustomInfoValidation.Failed(
                    PayoutProfileMessages.Text("Payout_BusinessUrl_Invalid"),
                    nameof(TenantPaymentProfile.BusinessUrl)));
            }

            return Task.FromResult(CustomInfoValidation.Ok());
        }

        /// <inheritdoc />
        public async Task<JsonNode> LoadAsync(int tenantId, CancellationToken ct)
        {
            var profile = await db.TenantPaymentProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);
            if (profile == null)
            {
                // null heisst im Vertrag "zu diesem Mandanten liegt nichts vor". Die Vertrags-Bibliothek hat
                // keinen Nullable-Kontext, darum das Ausrufezeichen an der Kante.
                return null!;
            }

            return new JsonObject
            {
                [nameof(TenantPaymentProfile.Country)] = profile.Country,
                [nameof(TenantPaymentProfile.EntityType)] = profile.EntityType,
                [nameof(TenantPaymentProfile.ContactEmail)] = profile.ContactEmail,
                [nameof(TenantPaymentProfile.DisplayName)] = profile.DisplayName,
                [nameof(TenantPaymentProfile.BusinessUrl)] = profile.BusinessUrl
            };
        }

        /// <summary>
        /// Writes the entries. Repeatable on purpose - the contract says so, and the row is addressed by tenant,
        /// so a second run updates instead of adding a twin.
        /// </summary>
        public async Task PersistAsync(CustomInfoPersistContext ctx, CancellationToken ct)
        {
            var profile = await db.TenantPaymentProfiles.FirstOrDefaultAsync(p => p.TenantId == ctx.TenantId, ct);
            if (profile == null)
            {
                profile = new TenantPaymentProfile { TenantId = ctx.TenantId, Created = DateTime.UtcNow };
                db.TenantPaymentProfiles.Add(profile);
            }

            var values = ctx.Fields ?? new Dictionary<string, string>();
            profile.Country = Nullable(Value(values, nameof(TenantPaymentProfile.Country))?.ToUpperInvariant());
            profile.EntityType = Nullable(Value(values, nameof(TenantPaymentProfile.EntityType))?.ToLowerInvariant());
            profile.ContactEmail = Nullable(Value(values, nameof(TenantPaymentProfile.ContactEmail)));
            profile.DisplayName = Nullable(Value(values, nameof(TenantPaymentProfile.DisplayName)));
            profile.BusinessUrl = Nullable(Value(values, nameof(TenantPaymentProfile.BusinessUrl)));
            profile.Updated = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            // The account is created from exactly these entries. Changing them after one exists does NOT reach
            // the provider - the country in particular is fixed there forever - so the trace has to say that.
            var hasAccount = await db.TenantPaymentAccounts.AnyAsync(a => a.TenantId == ctx.TenantId, ct);
            if (hasAccount)
            {
                LogEnvironment.LogEvent(
                    $"The payout entries of tenant {ctx.TenantId} were changed while a connected account already exists. They do not reach the provider by themselves; the account keeps the values it was created with.",
                    LogSeverity.Warning, "TenantPayments");
            }
        }

        /// <summary>The trimmed value of a field, never null.</summary>
        private static string Value(IReadOnlyDictionary<string, string>? values, string name)
            => values != null && values.TryGetValue(name, out var value) ? value?.Trim() ?? string.Empty : string.Empty;

        /// <summary>An empty entry is stored as absent, not as an empty string - the readers check for null.</summary>
        private static string? Nullable(string? value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>Performs application-defined tasks associated with freeing or resetting resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>Informs a calling class of a Disposal of this Instance</summary>
        public event EventHandler? Disposed;

        /// <summary>Raises the Disposed event</summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}
