using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Payrexx.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Die Abo-Kasse bei Payrexx — dieselbe Zahlungsseite wie beim Einzelverkauf, nur mit
    /// Abo-Angaben daran.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Das ist der angenehme Fall.</b> Payrexx hat keine eigene Abo-Welt: Ein Abo ist eine
    /// Zahlungsseite mit <c>subscriptionState</c> und einem Intervall. Kein Produktkatalog, keine
    /// Versionierung, kein Abonnent, kein Token — nichts von dem, was bei wallee drei Aufrufe braucht.
    /// </para>
    /// <para>
    /// <b>Der Preis dafür:</b> Was hier gebucht wird, steht nur auf dieser Zahlungsseite. Es gibt beim
    /// Anbieter kein Produkt, gegen das sich ein Plan abgleichen liesse — siehe
    /// <see cref="PayrexxPlanSynchronizer{TContext}"/>. Und weil der Betrag mitreist, ist die Summe aus
    /// Plan und Zusätzen <b>hier</b> zu bilden; Payrexx kennt keine Positionen, die es selbst addiert.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Konto gelaufen.</b>
    /// </para>
    /// </remarks>
    public class PayrexxSubscriptionCheckoutFactory<TContext> : ISubscriptionCheckoutFactory
        where TContext : DbContext, IBillingContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly PayrexxApiClient api;

        /// <summary>Initializes a new instance of the <see cref="PayrexxSubscriptionCheckoutFactory{TContext}"/> class.</summary>
        public PayrexxSubscriptionCheckoutFactory(IDbContextFactory<TContext> dbFactory, PayrexxApiClient api)
        {
            this.dbFactory = dbFactory;
            this.api = api;
        }

        /// <inheritdoc />
        public async Task<string> CreateCheckoutSessionAsync(int tenantId, int planId,
            IReadOnlyCollection<int> addOnIds, string successUrl, string cancelUrl, string? currency = null,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var plan = await db.Plans.Include(p => p.Prices).AsNoTracking()
                           .FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
                       ?? throw new PayrexxApiException($"Plan {planId} does not exist.");

            if (plan.BillingInterval == BillingInterval.OneTime)
            {
                // Eine Einmalzahlung ist kein Abo. Sie hier durchzulassen erzeugte bei Payrexx eine
                // wiederkehrende Belastung fuer etwas, das einmal bezahlt gehoert.
                throw new PayrexxApiException(
                    $"Plan {planId} is a one-time plan and must not be booked as a subscription.");
            }

            var cur = (string.IsNullOrWhiteSpace(currency) ? plan.Prices.FirstOrDefault()?.Currency : currency)
                      ?.ToUpperInvariant()
                      ?? throw new PayrexxApiException($"Plan {planId} has no price and no currency was given.");

            var planAmount = plan.Prices.FirstOrDefault(p => string.Equals(p.Currency, cur, StringComparison.OrdinalIgnoreCase))?.Amount
                             ?? throw new PayrexxApiException($"Plan {planId} has no price in {cur}.");

            // Die Summe entsteht HIER. Payrexx bekommt einen Betrag, keine Positionen - was der Endkunde
            // auf der Rechnung unterscheiden soll, muss darum in den Verwendungszweck.
            var addOns = await ResolveAddOnsAsync(db, planId, addOnIds, cur, cancellationToken);
            var total = planAmount + addOns.Sum(a => a.Amount);
            var purpose = addOns.Count == 0
                ? plan.Name
                : $"{plan.Name} + {string.Join(", ", addOns.Select(a => a.Name))}";

            var payload = new Dictionary<string, object?>
            {
                ["amount"] = CurrencyMinorUnits.ToMinor(total, cur),
                ["currency"] = cur,
                ["purpose"] = purpose,
                ["referenceId"] = $"tenant:{tenantId}:plan:{planId}",
                ["successRedirectUrl"] = successUrl,
                ["failedRedirectUrl"] = cancelUrl,
                ["cancelRedirectUrl"] = cancelUrl,
                // Das ist der ganze Unterschied zum Einzelverkauf.
                ["subscriptionState"] = true,
                ["subscriptionInterval"] = ToInterval(plan.BillingInterval),
                ["subscriptionCancellationInterval"] = ToInterval(plan.BillingInterval)
            };

            if (plan.TrialDays is > 0)
            {
                // OFFEN, gegen die Sandbox zu pruefen: die oeffentliche Referenz nennt fuer die Testphase
                // kein eigenes Feld. subscriptionPeriod ist die LAUFZEIT, nicht die Probezeit - es hier
                // zweckzuentfremden waere ein Abo, das nach der Probezeit endet.
                LogEnvironment.LogEvent(
                    $"Plan {planId} grants {plan.TrialDays} trial days, but the public Payrexx gateway API documents no trial field. The subscription starts charging immediately — verify against a test account before offering trials with this provider.",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
            }

            if (api.Options.PaymentMeans is { Length: > 0 })
            {
                payload["pm"] = api.Options.PaymentMeans;
            }

            try
            {
                var gateway = await api.PostAsync<PayrexxGateway>("Gateway/", payload, cancellationToken)
                              ?? throw new PayrexxApiException("Payrexx accepted the subscription gateway but returned nothing.");
                return string.IsNullOrWhiteSpace(gateway.Link)
                    ? throw new PayrexxApiException($"Payrexx gateway {gateway.Id} came back without a payment link.")
                    : gateway.Link!;
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not start the Payrexx subscription checkout for tenant {tenantId}, plan {planId}: {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                throw;
            }
        }

        private static async Task<List<(string Name, decimal Amount)>> ResolveAddOnsAsync(TContext db, int planId,
            IReadOnlyCollection<int> addOnIds, string currency, CancellationToken cancellationToken)
        {
            var result = new List<(string, decimal)>();
            if (addOnIds.Count == 0)
            {
                return result;
            }

            var rows = await db.PlanAddOns.Where(pa => pa.PlanId == planId && addOnIds.Contains(pa.AddOnId))
                .Select(pa => new
                {
                    pa.AddOnId,
                    pa.AddOn!.Name,
                    Price = pa.Prices.FirstOrDefault(p => p.Currency.ToUpper() == currency)
                }).ToListAsync(cancellationToken);

            foreach (var addOnId in addOnIds)
            {
                var row = rows.FirstOrDefault(r => r.AddOnId == addOnId);
                if (row?.Price == null)
                {
                    // Uebergangen, aber nicht stillschweigend: sonst zahlt der Mandant fuer weniger, als er
                    // gewaehlt hat, und niemand kann nachvollziehen, warum.
                    LogEnvironment.LogEvent(
                        $"Add-on {addOnId} was requested for plan {planId} but has no price in {currency}; it is left out of the subscription amount.",
                        LogSeverity.Warning, PayrexxApiClient.LogContext);
                    continue;
                }

                result.Add((row.Name, row.Price.Amount));
            }

            return result;
        }

        /// <summary>Das Intervall, wie Payrexx es benennt.</summary>
        private static string ToInterval(BillingInterval interval) => interval switch
        {
            BillingInterval.Yearly => "P1Y",
            _ => "P1M"
        };
    }

    /// <summary>
    /// Der Plan-Abgleich für Payrexx — es gibt nichts abzugleichen.
    /// </summary>
    /// <remarks>
    /// <b>Das ist kein vergessener Stummel.</b> Payrexx kennt keinen Produktkatalog über die API: Betrag,
    /// Bezeichnung und Intervall reisen mit jeder Zahlungsseite mit. Es gibt also beim Anbieter nichts,
    /// wogegen ein Plan abgeglichen werden könnte, und entsprechend keine <c>ProviderProductId</c>.
    /// <para>
    /// Die Methoden melden das einmal je Aufruf ins Protokoll, statt stumm zurückzukehren — wer einen
    /// Abgleich anstösst und nichts geschieht, soll den Grund finden.
    /// </para>
    /// </remarks>
    public class PayrexxPlanSynchronizer<TContext> : IPlanSynchronizer
        where TContext : DbContext, IBillingContext
    {
        /// <inheritdoc />
        public Task SyncPlanAsync(int planId, CancellationToken cancellationToken = default)
        {
            LogEnvironment.LogEvent(
                $"Plan {planId} needs no synchronisation with Payrexx: the provider has no product catalogue, the amount travels with each payment page.",
                LogSeverity.Report, PayrexxApiClient.LogContext);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SyncAddOnAsync(int addOnId, CancellationToken cancellationToken = default)
        {
            LogEnvironment.LogEvent(
                $"Add-on {addOnId} needs no synchronisation with Payrexx: the provider has no product catalogue, the amount travels with each payment page.",
                LogSeverity.Report, PayrexxApiClient.LogContext);
            return Task.CompletedTask;
        }
    }
}
