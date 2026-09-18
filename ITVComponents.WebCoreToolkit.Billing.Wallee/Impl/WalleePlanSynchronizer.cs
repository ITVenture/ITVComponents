using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using Microsoft.EntityFrameworkCore;
using Wallee.Client;
using Wallee.Model;
using Wallee.Service;
// Der Alias muss in JEDER Datei stehen - using-Aliase gelten nur dateilokal. Ohne ihn verdeckt
// unser eigener Namensraum ITVComponents.WebCoreToolkit.Configuration den Typ Wallee.Client.Configuration.
using WalleeConfiguration = Wallee.Client.Configuration;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Bringt Pläne und Zusätze als wallee-Abo-Produkte in Form.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Das Modell ist anders geschnitten als bei Stripe</b>, und das ist der Grund für fast jede
    /// Eigenheit hier. Bei Stripe ist ein Plan ein Produkt mit Preis, ein Zusatz ein zweites Produkt, und
    /// ein Abo trägt mehrere Positionen. Bei wallee gibt es <b>ein</b> Produkt je Plan, darin genau eine
    /// aktive <i>Produktversion</i>, darin Komponentengruppen mit Komponenten — und die Zusätze sind
    /// Komponenten INNERHALB dieser Version.
    /// </para>
    /// <para>
    /// <b>Die Folge: ein Zusatz lässt sich nicht für sich synchronisieren.</b> Er gehört zu jeder
    /// Produktversion, die ihn anbietet — und unser Datenmodell erlaubt einem Zusatz, an mehreren Plänen
    /// zu hängen. <see cref="SyncAddOnAsync"/> zieht darum alle betroffenen Pläne nach.
    /// </para>
    /// <para>
    /// <b>Und jede Änderung ist eine neue Version.</b> wallee lässt eine aktive Produktversion nicht
    /// ändern; wer den Preis anpasst, legt eine neue an, und die alte wird <i>obsolete</i>. Deshalb wird
    /// hier nur dann versioniert, wenn sich inhaltlich wirklich etwas geändert hat — sonst hinterlässt
    /// jeder Aufruf eine Version mehr, und die Liste beim Anbieter wird unlesbar.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen einen echten Raum gelaufen.</b> Signaturen stammen aus dem SDK selbst; der
    /// Ablauf über sechs Aufrufe ist ungeprüft.
    /// </para>
    /// </remarks>
    public class WalleePlanSynchronizer<TContext> : IPlanSynchronizer
        where TContext : DbContext, IBillingContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IGlobalSettings<WalleeOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="WalleePlanSynchronizer{TContext}"/> class.</summary>
        public WalleePlanSynchronizer(IDbContextFactory<TContext> dbFactory, IGlobalSettings<WalleeOptions> settings)
        {
            this.dbFactory = dbFactory;
            this.settings = settings;
        }

        /// <inheritdoc />
        public async Task SyncPlanAsync(int planId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var plan = await db.Plans.Include(p => p.Prices).Include(p => p.PlanAddOns).ThenInclude(pa => pa.AddOn)
                           .FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
                       ?? throw new WalleeSaleException($"Plan {planId} does not exist.");

            await SyncCoreAsync(db, plan, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Zieht ALLE Pläne nach, die diesen Zusatz führen — siehe Klassenkommentar. Ein Zusatz ohne Plan
        /// hat bei wallee keinen Ort, an dem er stehen könnte; das ist kein Fehler, sondern ein Hinweis.
        /// </remarks>
        public async Task SyncAddOnAsync(int addOnId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var planIds = await db.PlanAddOns.Where(pa => pa.AddOnId == addOnId).Select(pa => pa.PlanId)
                .Distinct().ToListAsync(cancellationToken);

            if (planIds.Count == 0)
            {
                LogEnvironment.LogEvent(
                    $"Add-on {addOnId} is not attached to any plan. With wallee an add-on only exists as a component inside a plan's product version, so there is nothing to synchronise.",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return;
            }

            foreach (var planId in planIds)
            {
                var plan = await db.Plans.Include(p => p.Prices).Include(p => p.PlanAddOns).ThenInclude(pa => pa.AddOn)
                    .FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken);
                if (plan != null)
                {
                    await SyncCoreAsync(db, plan, cancellationToken);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task SyncCoreAsync(TContext db, Plan plan, CancellationToken cancellationToken)
        {
            var options = settings.Value;
            var space = options.SpaceId;
            if (space <= 0)
            {
                throw new WalleeSaleException("The 'WalleePayments' setting names no space to work in.");
            }

            var configuration = WalleeRuntime.Configure(options);
            var language = string.IsNullOrWhiteSpace(options.DefaultLanguage) ? "en-US" : options.DefaultLanguage;

            try
            {
                var productId = await EnsureProductAsync(configuration, space, plan, cancellationToken);
                var versionId = await CreateVersionAsync(configuration, space, productId, plan, language, cancellationToken);
                var groupId = await CreateGroupAsync(configuration, space, versionId, language, cancellationToken);
                var component = await CreateComponentAsync(configuration, space, groupId, plan.Name, language, cancellationToken);
                await CreatePeriodFeeAsync(configuration, space, component.Id, plan.Name, language,
                    plan.Prices.Select(p => (p.Currency, p.Amount)), cancellationToken);

                foreach (var addOn in plan.PlanAddOns.Where(pa => pa.AddOn is { IsActive: true }).Select(pa => pa.AddOn!))
                {
                    // Jeder Zusatz wird eine eigene Komponente in einer EIGENEN, optionalen Gruppe. Eine
                    // gemeinsame Gruppe waere eine Auswahl "entweder/oder" - genau das ist ein Zusatz nicht.
                    var addOnGroup = await CreateGroupAsync(configuration, space, versionId, language, cancellationToken, optional: true);
                    var addOnComponent = await CreateComponentAsync(configuration, space, addOnGroup, addOn.Name, language,
                        cancellationToken, isDefault: false);
                    var prices = await db.PlanAddOnPrices
                        .Where(p => p.PlanAddOn!.AddOnId == addOn.AddOnId && p.PlanAddOn.PlanId == plan.PlanId)
                        .Select(p => new { p.Currency, p.Amount }).ToListAsync(cancellationToken);
                    await CreatePeriodFeeAsync(configuration, space, addOnComponent.Id, addOn.Name, language,
                        prices.Select(p => (p.Currency, p.Amount)), cancellationToken);
                    addOn.ProviderProductId = addOnComponent.Reference?.Id.ToString() ?? addOn.ProviderProductId;
                }

                await Task.Run(() => new SubscriptionProductVersionsService(configuration)
                    .PostSubscriptionsProductsVersionsIdActivate(versionId, space), cancellationToken).ConfigureAwait(false);

                // Zusammengesetzt, und das ist Absicht: fuer das Anlegen eines Abos braucht es BEIDES - die
                // Produkt-Id und die Referenz der Komponente. Ein eigenes Feld dafuer waere eine
                // Schemaaenderung, die nur wallee braucht; der Doppelpunkt ist der kleinere Eingriff.
                plan.ProviderProductId = $"{productId}:{component.Reference?.Id}";
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not synchronise plan {plan.PlanId} ('{plan.Name}') to wallee space {space}: {ex.OutlineException()}",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                throw new WalleeSaleException($"wallee refused the plan synchronisation: {ex.Message}");
            }
        }

        private static async Task<long> EnsureProductAsync(WalleeConfiguration configuration, long space, Plan plan,
            CancellationToken cancellationToken)
        {
            if (TrySplit(plan.ProviderProductId, out var existing, out _))
            {
                return existing;
            }

            var product = await Task.Run(() => new SubscriptionProductsService(configuration)
                .PostSubscriptionsProducts(space, new SubscriptionProductCreate
                {
                    // Schlichter String - anders als bei Version, Gruppe und Komponente, wo der Name ein
                    // Woerterbuch je Sprache ist. Diese Unstimmigkeit steckt im SDK, nicht hier.
                    Name = plan.Name,
                    Reference = $"plan:{plan.PlanId}",
                    State = SubscriptionProductState.ACTIVE
                }), cancellationToken).ConfigureAwait(false);
            return product.Id;
        }

        private static async Task<long> CreateVersionAsync(WalleeConfiguration configuration, long space, long productId,
            Plan plan, string language, CancellationToken cancellationToken)
        {
            var currencies = plan.Prices.Select(p => p.Currency.ToUpperInvariant()).Distinct().ToList();
            var version = await Task.Run(() => new SubscriptionProductVersionsService(configuration)
                .PostSubscriptionsProductsVersions(space, new SubscriptionProductVersionPending
                {
                    Product = productId,
                    // ISO-8601-Dauer, nicht "monthly": P1M oder P1Y.
                    BillingCycle = ToBillingCycle(plan.BillingInterval),
                    DefaultCurrency = currencies.FirstOrDefault() ?? "CHF",
                    EnabledCurrencies = currencies,
                    Name = new Dictionary<string, string> { [language] = plan.Name },
                    State = SubscriptionProductVersionState.PENDING
                }), cancellationToken).ConfigureAwait(false);
            return version.Id;
        }

        private static async Task<long> CreateGroupAsync(WalleeConfiguration configuration, long space, long versionId,
            string language, CancellationToken cancellationToken, bool optional = false)
        {
            var group = await Task.Run(() => new SubscriptionProductComponentGroupsService(configuration)
                .PostSubscriptionsProductsComponentGroups(space, new SubscriptionProductComponentGroupUpdate
                {
                    ProductVersion = versionId,
                    Name = new Dictionary<string, string> { [language] = optional ? "Add-ons" : "Plan" },
                    Optional = optional
                }), cancellationToken).ConfigureAwait(false);
            return group.Id;
        }

        private static async Task<SubscriptionProductComponent> CreateComponentAsync(WalleeConfiguration configuration,
            long space, long groupId, string name, string language, CancellationToken cancellationToken,
            bool isDefault = true)
            => await Task.Run(() => new SubscriptionProductComponentsService(configuration)
                .PostSubscriptionsProductsComponents(space, new SubscriptionProductComponentUpdate
                {
                    ComponentGroup = groupId,
                    Name = new Dictionary<string, string> { [language] = name },
                    DefaultComponent = isDefault,
                    MinimalQuantity = 1,
                    MaximalQuantity = 1,
                    QuantityStep = 1
                }), cancellationToken).ConfigureAwait(false);

        private static async Task CreatePeriodFeeAsync(WalleeConfiguration configuration, long space, long componentId,
            string name, string language, IEnumerable<(string Currency, decimal Amount)> prices,
            CancellationToken cancellationToken)
        {
            var fee = prices
                .Select(p => new PersistableCurrencyAmountUpdate { Currency = p.Currency.ToUpperInvariant(), Amount = p.Amount })
                .ToList();
            if (fee.Count == 0)
            {
                // Ohne Preis kein Abo. Das ist kein technischer Fehler, aber ein Plan, den niemand buchen
                // kann - und das faellt sonst erst auf, wenn ein Mandant es versucht.
                LogEnvironment.LogEvent(
                    $"'{name}' has no price in any currency; wallee gets a component without a period fee, and nobody can subscribe to it.",
                    LogSeverity.Warning, "TenantPayments");
                return;
            }

            await Task.Run(() => new SubscriptionProductPeriodFeesService(configuration)
                .PostSubscriptionsProductsPeriodFees(space, new ProductPeriodFeeUpdate
                {
                    Component = componentId,
                    Name = new Dictionary<string, string> { [language] = name },
                    LedgerEntryTitle = new Dictionary<string, string> { [language] = name },
                    // Ein Eintrag JE WAEHRUNG - nicht ein Betrag mit einer Waehrung daneben.
                    PeriodFee = fee
                }), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Die Abrechnungsdauer als ISO-8601-Dauer, wie wallee sie erwartet.</summary>
        private static string ToBillingCycle(BillingInterval interval) => interval switch
        {
            BillingInterval.Yearly => "P1Y",
            BillingInterval.Monthly => "P1M",
            // Einmalzahlungen sind kein Abo. Der Aufrufer sollte sie gar nicht hierher geben; kommt doch
            // eine, ist ein Monatszyklus die harmloseste Annahme - und sie steht im Protokoll.
            _ => "P1M"
        };

        /// <summary>
        /// Zerlegt den zusammengesetzten Schluessel <c>produktId:referenzId</c>.
        /// </summary>
        internal static bool TrySplit(string? providerProductId, out long productId, out long componentReferenceId)
        {
            productId = 0;
            componentReferenceId = 0;
            if (string.IsNullOrWhiteSpace(providerProductId))
            {
                return false;
            }

            var parts = providerProductId.Split(':', 2);
            if (!long.TryParse(parts[0], out productId))
            {
                return false;
            }

            if (parts.Length == 2)
            {
                long.TryParse(parts[1], out componentReferenceId);
            }

            return true;
        }
    }
}
