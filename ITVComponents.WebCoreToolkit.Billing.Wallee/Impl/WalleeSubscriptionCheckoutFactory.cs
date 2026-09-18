using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
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
    /// Die Abo-Kasse bei wallee — drei Aufrufe statt eines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es gibt keine gehostete Abo-Zahlungsseite.</b> Der Weg dorthin führt über die erste Ladung:
    /// </para>
    /// <code>
    /// PostSubscriptions(...)                       -> SubscriptionVersion
    /// PostSubscriptionsIdInitializeSubscriberPresent -> SubscriptionCharge (mit Transaction)
    /// GetPaymentTransactionsIdPaymentPageUrl(txId)  -> die Adresse
    /// </code>
    /// <para>
    /// „SubscriberPresent" ist dabei der entscheidende Teil: es ist die Fassung für den Fall, dass der
    /// Zahlende gerade davorsitzt und etwas eingeben kann. Die schlichte <c>Initialize</c>-Fassung
    /// verlangt ein bereits hinterlegtes Zahlungsmittel und führt zu keiner Seite.
    /// </para>
    /// <para>
    /// <b>Ein Abo braucht einen Abonnenten</b> (Pflichtfeld), den es bei uns nicht gibt — wir kennen nur
    /// Mandanten. Er wird darum bei Bedarf angelegt und über <c>ExternalId</c> wiedergefunden. Diese
    /// Kennung lässt sich nur beim ANLEGEN setzen, später nicht mehr — wer sie vergisst, findet den
    /// Abonnenten nie wieder und legt bei jedem Anlauf einen neuen an.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen einen echten Raum gelaufen.</b>
    /// </para>
    /// </remarks>
    public class WalleeSubscriptionCheckoutFactory<TContext> : ISubscriptionCheckoutFactory
        where TContext : DbContext, IBillingContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IGlobalSettings<WalleeOptions> settings;

        /// <summary>Initializes a new instance of the <see cref="WalleeSubscriptionCheckoutFactory{TContext}"/> class.</summary>
        public WalleeSubscriptionCheckoutFactory(IDbContextFactory<TContext> dbFactory,
            IGlobalSettings<WalleeOptions> settings)
        {
            this.dbFactory = dbFactory;
            this.settings = settings;
        }

        /// <inheritdoc />
        public async Task<string> CreateCheckoutSessionAsync(int tenantId, int planId,
            IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, string? currency = null,
            CancellationToken cancellationToken = default)
        {
            var options = settings.Value;
            var space = options.SpaceId;
            if (space <= 0)
            {
                throw new WalleeSaleException("The 'WalleePayments' setting names no space to work in.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var plan = await db.Plans.Include(p => p.Prices).AsNoTracking()
                           .FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
                       ?? throw new WalleeSaleException($"Plan {planId} does not exist.");

            if (!WalleePlanSynchronizer<TContext>.TrySplit(plan.ProviderProductId, out var productId, out var componentReferenceId)
                || componentReferenceId == 0)
            {
                // Der zusammengesetzte Schluessel entsteht beim Synchronisieren. Fehlt er, ist der Plan
                // beim Anbieter nicht bekannt - und das ist eine andere Lage als ein abgelehntes Abo.
                throw new WalleeSaleException(
                    $"Plan {planId} has not been synchronised to wallee yet (ProviderProductId '{plan.ProviderProductId}' carries no component reference).");
            }

            var cur = (string.IsNullOrWhiteSpace(currency)
                ? plan.Prices.FirstOrDefault()?.Currency
                : currency)?.ToUpperInvariant()
                ?? throw new WalleeSaleException($"Plan {planId} has no price and no currency was given.");

            var configuration = WalleeRuntime.Configure(options);

            try
            {
                var subscriberId = await EnsureSubscriberAsync(configuration, space, tenantId, cancellationToken);

                var components = new List<SubscriptionComponentReferenceConfiguration>
                {
                    new() { ProductComponentReferenceId = componentReferenceId, Quantity = 1 }
                };
                components.AddRange(await ResolveAddOnComponentsAsync(db, addOnIds, planId, cancellationToken));

                var version = await Task.Run(() => new SubscriptionsService(configuration).PostSubscriptions(space,
                    new SubscriptionCreateRequest
                    {
                        Product = productId,
                        Currency = cur,
                        ComponentConfigurations = components,
                        Subscription = new SubscriptionPending
                        {
                            Subscriber = subscriberId,
                            Reference = $"tenant:{tenantId}:plan:{planId}"
                        }
                    },
                    // expand: ohne das kommt die Antwort mit einer NICHT ausgeklappten Subscription, und
                    // die naechste Zeile laeuft in eine Nullreferenz. Die Version selbst hat zwar auch eine
                    // Id - die ist aber die der VERSION, nicht die des Abos, und der Aufruf darunter
                    // verlangt letztere.
                    ["subscription"]), cancellationToken).ConfigureAwait(false);

                if (version.Subscription == null)
                {
                    throw new WalleeSaleException(
                        $"wallee created a subscription version for tenant {tenantId} but did not expand the subscription, so its id is unknown.");
                }

                var charge = await Task.Run(() => new SubscriptionsService(configuration)
                    .PostSubscriptionsIdInitializeSubscriberPresent(version.Subscription.Id, space,
                        new SubscriptionInitializeSubscriberPresentRequest
                        {
                            SuccessUrl = successUrl,
                            // ACHTUNG: hier heisst es FailureUrl. Bei SubscriptionChargeCreate daneben
                            // heisst dasselbe FailedUrl. Verwechselt bleibt das Feld schlicht leer.
                            FailureUrl = cancelUrl
                        }), cancellationToken).ConfigureAwait(false);

                if (charge.Transaction == null)
                {
                    throw new WalleeSaleException(
                        $"wallee created the subscription charge for tenant {tenantId} but attached no transaction, so there is no payment page.");
                }

                var url = await Task.Run(() => new TransactionsService(configuration)
                    .GetPaymentTransactionsIdPaymentPageUrl(charge.Transaction.Id, space), cancellationToken)
                    .ConfigureAwait(false);

                return string.IsNullOrWhiteSpace(url)
                    ? throw new WalleeSaleException($"wallee returned no payment page URL for transaction {charge.Transaction.Id}.")
                    : url;
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not start the wallee subscription checkout for tenant {tenantId}, plan {planId} (space {space}): {ex.OutlineException()}",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                throw new WalleeSaleException($"wallee refused the subscription checkout: {ex.Message}");
            }
        }

        /// <summary>
        /// Der Abonnent des Mandanten - angelegt, falls es ihn noch nicht gibt.
        /// </summary>
        /// <remarks>
        /// Gesucht wird ueber die Liste und <c>ExternalId</c>. Das ist nicht schoen, aber der einzige Weg:
        /// das SDK bietet kein Suchen nach dieser Kennung, und die Alternative waere, sie bei uns zu
        /// speichern - also ein Feld im Schema, das nur wallee braucht.
        /// </remarks>
        private static async Task<long> EnsureSubscriberAsync(WalleeConfiguration configuration, long space,
            int tenantId, CancellationToken cancellationToken)
        {
            var externalId = $"tenant:{tenantId}";
            var service = new SubscribersService(configuration);

            var existing = await Task.Run(() => service.GetSubscriptionsSubscribers(space, limit: 100), cancellationToken)
                .ConfigureAwait(false);
            // ueber .Data und nicht direkt: KEINE der rund neunzig *ListResponse-Klassen des SDK ist
            // enumerierbar. Direkt aufgerufen bindet LINQ an eine fremde Erweiterung und meldet einen
            // Typrueckschluss-Fehler, der nichts mit der eigentlichen Ursache zu tun hat.
            var match = existing?.Data?.FirstOrDefault(s => string.Equals(s.ExternalId, externalId, StringComparison.Ordinal));
            if (match != null)
            {
                return match.Id;
            }

            var created = await Task.Run(() => service.PostSubscriptionsSubscribers(space, new SubscriberCreate
            {
                // Nur HIER setzbar. SubscriberUpdate kennt das Feld nicht mehr - ein Abonnent ohne
                // ExternalId ist fuer uns fuer immer unauffindbar.
                ExternalId = externalId,
                Reference = externalId,
                State = CreationEntityState.ACTIVE
            }), cancellationToken).ConfigureAwait(false);
            return created.Id;
        }

        /// <summary>
        /// Die Komponenten-Referenzen der gewaehlten Zusaetze. Ein Zusatz, der zu diesem Plan nicht
        /// gehoert oder nicht synchronisiert ist, wird uebergangen - aber nicht stillschweigend.
        /// </summary>
        private static async Task<List<SubscriptionComponentReferenceConfiguration>> ResolveAddOnComponentsAsync(
            TContext db, IReadOnlyCollection<int> addOnIds, int planId, CancellationToken cancellationToken)
        {
            var result = new List<SubscriptionComponentReferenceConfiguration>();
            if (addOnIds.Count == 0)
            {
                return result;
            }

            var addOns = await db.PlanAddOns.Where(pa => pa.PlanId == planId && addOnIds.Contains(pa.AddOnId))
                .Select(pa => new { pa.AddOnId, pa.AddOn!.Name, pa.AddOn.ProviderProductId })
                .ToListAsync(cancellationToken);

            foreach (var addOnId in addOnIds)
            {
                var addOn = addOns.FirstOrDefault(a => a.AddOnId == addOnId);
                if (addOn == null)
                {
                    LogEnvironment.LogEvent(
                        $"Add-on {addOnId} was requested for plan {planId} but is not attached to it; it is left out of the subscription.",
                        LogSeverity.Warning, "TenantPayments");
                    continue;
                }

                if (!long.TryParse(addOn.ProviderProductId, out var reference) || reference == 0)
                {
                    LogEnvironment.LogEvent(
                        $"Add-on {addOnId} ('{addOn.Name}') has no wallee component reference yet — synchronise the plan first. It is left out of the subscription.",
                        LogSeverity.Warning, "TenantPayments");
                    continue;
                }

                result.Add(new SubscriptionComponentReferenceConfiguration
                {
                    ProductComponentReferenceId = reference,
                    Quantity = 1
                });
            }

            return result;
        }
    }

    /// <summary>
    /// Das Selbstbedienungs-Portal — bei wallee gibt es keines.
    /// </summary>
    /// <remarks>
    /// Kein Typ und keine Methode der gesamten SDK-Oberfläche bietet einen Abonnenten-Zugang;
    /// <c>SubscribersService</c> ist reine Verwaltung. Was Stripe dort anbietet — Plan wechseln,
    /// Zahlungsmittel ändern, kündigen — müsste hier selbst gebaut werden, auf
    /// <c>PostSubscriptionsIdApplyChanges</c>, <c>...IdTerminate</c> und einer Token-Aktualisierung.
    /// <para>
    /// Diese Klasse gibt darum <b>null</b> zurück und sagt es. Ein Stub, der eine Adresse erfindet oder
    /// stumm nichts tut, würde die Lücke verdecken, bis ein Mandant vor einer toten Schaltfläche steht.
    /// </para>
    /// </remarks>
    public class WalleeBillingPortalFactory : IBillingPortalFactory
    {
        /// <inheritdoc />
        public Task<string?> CreatePortalSessionAsync(int tenantId, string returnUrl,
            CancellationToken cancellationToken = default)
        {
            LogEnvironment.LogEvent(
                $"A billing portal was requested for tenant {tenantId}, but wallee has no subscriber self-service. Hide the entry point or build the plan/payment-method/cancellation screens yourself.",
                LogSeverity.Warning, "TenantPayments");
            return Task.FromResult<string?>(null);
        }
    }
}
