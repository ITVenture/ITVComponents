using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Was eine eingehende Zahlungsmeldung an der Verkaufszeile bewirkt — für jeden Anbieter dasselbe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Anbieter-Teil eines Webhooks ist das Prüfen der Signatur und das Auspacken seines Formats.
    /// Was danach passiert, ist überall gleich — und es ist die Hälfte, bei der Fehler Geld kosten:
    /// </para>
    /// <list type="bullet">
    /// <item><b>Eine Meldung kommt mehrfach.</b> Jeder dieser Anbieter liefert erneut, wenn die Antwort
    /// ausbleibt. Ein Verkauf darf trotzdem nur EINMAL freigegeben werden.</item>
    /// <item><b>Ein bezahlter Verkauf wird nie wieder ungültig.</b> Eine verspätete Abbruch-Meldung darf
    /// eine Zahlung nicht zurücknehmen.</item>
    /// <item><b>Erstattungen kommen aus zwei Richtungen</b> — von uns ausgelöst oder im Portal des
    /// Anbieters. Nur die unbekannten werden nachgetragen, sonst zählt eine doppelt.</item>
    /// </list>
    /// <para>
    /// Dreimal nachgebaut liefe das auseinander, und zwar leise: der Unterschied zeigt sich erst in einer
    /// Abrechnung, die nicht aufgeht.
    /// </para>
    /// </remarks>
    public class TenantSaleWebhookSink<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly TenantSaleNotifier notifier;

        /// <summary>Initializes a new instance of the <see cref="TenantSaleWebhookSink{TContext}"/> class.</summary>
        public TenantSaleWebhookSink(IDbContextFactory<TContext> dbFactory, TenantSaleNotifier notifier)
        {
            this.dbFactory = dbFactory;
            this.notifier = notifier;
        }

        /// <summary>
        /// Sucht den Verkauf, den eine Meldung meint.
        /// </summary>
        /// <param name="providerSessionId">
        /// die Kennung, die der Anbieter beim Anlegen vergeben hat. <b>Der verlässliche Weg</b> — sie ist
        /// beim Anbieter eindeutig und steht auf unserer Zeile.
        /// </param>
        /// <param name="externalReference">
        /// unsere eigene Referenz, als zweiter Versuch. Sie ist nur JE MANDANT eindeutig, darum nur
        /// zusammen mit <paramref name="tenantId"/> brauchbar.
        /// </param>
        /// <param name="tenantId">der Mandant, falls die Meldung ihn nennt</param>
        public async Task<TenantSale?> FindSaleAsync(TContext db, string? providerSessionId,
            string? externalReference, int? tenantId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(providerSessionId))
            {
                var bySession = await db.TenantSales.Include(s => s.Refunds)
                    .FirstOrDefaultAsync(s => s.ProviderSessionId == providerSessionId, cancellationToken);
                if (bySession != null)
                {
                    return bySession;
                }
            }

            if (string.IsNullOrWhiteSpace(externalReference))
            {
                return null;
            }

            if (tenantId is > 0)
            {
                return await db.TenantSales.Include(s => s.Refunds)
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ExternalReference == externalReference,
                        cancellationToken);
            }

            // Ohne Mandant ist die Referenz nicht eindeutig. Lieber nichts zurueckgeben als den Verkauf
            // eines FREMDEN Mandanten zu bezahlen, nur weil er dieselbe Bestellnummer vergeben hat.
            var candidates = await db.TenantSales.Include(s => s.Refunds)
                .Where(s => s.ExternalReference == externalReference).Take(2).ToListAsync(cancellationToken);
            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            if (candidates.Count > 1)
            {
                LogEnvironment.LogEvent(
                    $"A payment notification carries reference '{externalReference}' without a tenant, and more than one tenant uses it. The notification is ignored — guessing here would pay a stranger's order.",
                    LogSeverity.Error, LogContext);
            }

            return null;
        }

        /// <summary>
        /// Bucht den Verkauf als bezahlt. Mehrfach aufzurufen ist ausdrücklich erlaubt.
        /// </summary>
        /// <param name="chargeId">
        /// die Kennung der Zahlung beim Anbieter — <b>die, mit der später erstattet wird</b>. Bei Payrexx
        /// ist das die UUID der Transaktion, nicht ihre Nummer.
        /// </param>
        /// <param name="customerEmail">die vom Anbieter erfasste Adresse, oder null</param>
        /// <returns>true, wenn dieser Aufruf den Verkauf freigegeben hat — false bei einer Wiederholung</returns>
        public async Task<bool> MarkPaidAsync(TenantSale sale, TContext db, string? chargeId,
            string? customerEmail, CancellationToken cancellationToken)
        {
            // Die Kennungen werden AUCH bei einer Wiederholung nachgetragen: die erste Meldung kennt sie
            // manchmal noch nicht, und ohne sie laesst sich spaeter nicht erstatten.
            sale.ProviderChargeId ??= chargeId;
            if (string.IsNullOrWhiteSpace(sale.CustomerEmail) && !string.IsNullOrWhiteSpace(customerEmail))
            {
                sale.CustomerEmail = customerEmail;
            }

            if (sale.Status != TenantSaleStatus.Pending)
            {
                // Schon gebucht. Neue Kennungen sichern, aber die Ware nicht ein zweites Mal freigeben.
                sale.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return false;
            }

            sale.Status = TenantSaleStatus.Paid;
            sale.PaidUtc = DateTime.UtcNow;
            // Die Zahlungsseite ist verbraucht. Stehen zu lassen hiesse, dem Kunden einen Link zu zeigen,
            // der ihn ein zweites Mal zahlen lassen koennte.
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            await notifier.NotifyCompletedAsync(sale, cancellationToken);
            return true;
        }

        /// <summary>
        /// Bringt einen UNBEZAHLTEN Verkauf in einen Endzustand (abgebrochen, fehlgeschlagen, abgelaufen).
        /// </summary>
        /// <remarks>
        /// Ein bezahlter bleibt unberührt — Meldungen überholen sich, und eine verspätete Absage darf eine
        /// Zahlung nicht zurücknehmen.
        /// </remarks>
        public async Task MoveToAsync(TenantSale sale, TContext db, TenantSaleStatus status,
            CancellationToken cancellationToken)
        {
            if (sale.Status != TenantSaleStatus.Pending)
            {
                return;
            }

            sale.Status = status;
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Trägt eine Erstattung nach, die beim Anbieter ausgelöst wurde und uns noch fehlt.
        /// </summary>
        /// <remarks>
        /// Erstattungen, die wir selbst gebucht haben, stehen schon da und werden übersprungen — das ist
        /// zugleich, was die Beobachter davon abhält, zweimal zu feuern.
        /// <para>
        /// Die zurückgegebene Provision bleibt hier auf 0. Wer sie kennt, nimmt
        /// <see cref="MirrorRefundAsync(TenantSale, TContext, MirroredRefund, CancellationToken)"/> —
        /// sie hier zu schätzen hiesse, eine Abrechnung zu erfinden: was im Portal eines Anbieters
        /// ausgelöst wurde, sagt nichts darüber, ob die Provision mitging.
        /// </para>
        /// </remarks>
        public Task MirrorRefundAsync(TenantSale sale, TContext db, string providerRefundId,
            long amountMinor, string? status, CancellationToken cancellationToken)
            => MirrorRefundAsync(sale, db,
                new MirroredRefund(providerRefundId, amountMinor, status, Reason: "mirrored from the provider"),
                cancellationToken);

        /// <summary>
        /// Trägt eine Erstattung nach, von der mehr bekannt ist als nur Betrag und Zustand.
        /// </summary>
        /// <remarks>
        /// Der Weg für Anbieter, die auch sagen, <b>wie viel Provision</b> zurückging — bei Stripe steht
        /// das auf der Gebühr der Plattform und ist damit eine Tatsache, keine Schätzung. Bei den anderen
        /// wird je Verkauf gar keine einbehalten, dort bleibt das Feld 0.
        /// </remarks>
        public async Task MirrorRefundAsync(TenantSale sale, TContext db, MirroredRefund refund,
            CancellationToken cancellationToken)
        {
            if (sale.Refunds.Any(r => string.Equals(r.ProviderRefundId, refund.ProviderRefundId, StringComparison.Ordinal)))
            {
                return;
            }

            var row = new TenantSaleRefund
            {
                TenantSaleId = sale.TenantSaleId,
                AmountMinor = refund.AmountMinor,
                ApplicationFeeRefundedMinor = refund.ApplicationFeeRefundedMinor,
                ProviderRefundId = refund.ProviderRefundId,
                Reason = refund.Reason,
                Status = refund.Status,
                Created = refund.Created ?? DateTime.UtcNow
            };
            sale.Refunds.Add(row);
            sale.Status = TenantSaleServiceBase<TContext>.DeriveStatus(sale.AmountMinor,
                sale.Refunds.Sum(r => r.AmountMinor));
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            await notifier.NotifyRefundedAsync(sale, row, cancellationToken);
        }

        /// <summary>
        /// Gleicht die Erstattungen an einen <b>Gesamtstand</b> an, den der Anbieter meldet.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der Weg für Anbieter, die keine einzelne Erstattung melden, sondern nur den neuen Zustand der
        /// Transaktion — Payrexx etwa sagt <c>partially-refunded</c> und nennt daneben, wie viel insgesamt
        /// zurückging. Was fehlt, ist die <b>Differenz</b> zum lokal bekannten Stand.
        /// </para>
        /// <para>
        /// Die Kennung der nachgetragenen Zeile enthält diesen Gesamtstand. Damit ergibt dieselbe Meldung
        /// zweimal dieselbe Kennung und wird oben abgefangen — während eine ZWEITE, echte Teilerstattung
        /// einen anderen Gesamtstand hat und durchkommt. Eine laufende Nummer täte beides nicht.
        /// </para>
        /// </remarks>
        /// <param name="totalRefundedMinor">was der Anbieter als insgesamt erstattet meldet</param>
        /// <param name="marker">
        /// ein kurzes Kürzel für die Herkunft, das in die synthetische Kennung eingeht (z.B. der Name des
        /// Anbieters)
        /// </param>
        public async Task MirrorRefundTotalAsync(TenantSale sale, TContext db, long totalRefundedMinor,
            string marker, string? status, CancellationToken cancellationToken)
        {
            var known = sale.Refunds.Sum(r => r.AmountMinor);
            var delta = totalRefundedMinor - known;
            if (delta <= 0)
            {
                // Entweder nichts Neues, oder der Anbieter meldet WENIGER als wir gebucht haben. Der zweite
                // Fall ist keiner, den ein Webhook aufloesen kann: eine Erstattung zurueckzunehmen, weil
                // eine Zahl kleiner ist als erwartet, waere schlimmer als die Abweichung selbst.
                if (delta < 0)
                {
                    LogEnvironment.LogEvent(
                        $"Sale {sale.TenantSaleId} has {known} booked as refunded, but the provider reports only {totalRefundedMinor}. Nothing is changed — a webhook must not take a refund back. Check this against the provider's books.",
                        LogSeverity.Warning, LogContext);
                }

                return;
            }

            await MirrorRefundAsync(sale, db, $"{marker}:total:{totalRefundedMinor}", delta, status,
                cancellationToken);
        }

        /// <summary>Öffnet einen Kontext für einen Webhook-Durchlauf.</summary>
        public Task<TContext> CreateContextAsync(CancellationToken cancellationToken)
            => dbFactory.CreateDbContextAsync(cancellationToken);

        internal const string LogContext = "TenantPayments";
    }

    /// <summary>
    /// Eine Erstattung, wie ein Anbieter sie meldet.
    /// </summary>
    /// <param name="ProviderRefundId">
    /// die Kennung beim Anbieter. <b>Sie allein entscheidet über die Doppelzählung</b> — dieselbe
    /// Kennung wird übersprungen, eine neue gebucht.
    /// </param>
    /// <param name="AmountMinor">der erstattete Betrag in der kleinsten Einheit</param>
    /// <param name="Status">der Zustand beim Anbieter, roh übernommen</param>
    /// <param name="ApplicationFeeRefundedMinor">
    /// wie viel Provision dabei zurückging. <b>Nur setzen, wenn der Anbieter es sagt</b> — bei Payrexx
    /// und wallee wird je Verkauf keine einbehalten, dort ist 0 die richtige Antwort und keine Lücke.
    /// </param>
    /// <param name="Reason">der Grund, soweit der Anbieter einen nennt</param>
    /// <param name="Created">
    /// wann die Erstattung entstand. Ohne Angabe gilt jetzt — was für eine gespiegelte Erstattung
    /// ungenau ist, aber nie in der Zukunft liegt.
    /// </param>
    public readonly record struct MirroredRefund(
        string ProviderRefundId,
        long AmountMinor,
        string? Status,
        long ApplicationFeeRefundedMinor = 0,
        string? Reason = null,
        DateTime? Created = null);
}
