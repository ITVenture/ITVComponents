using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
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

            if (!IsUnpaid(sale.Status))
            {
                // Schon gebucht. Neue Kennungen sichern, aber die Ware nicht ein zweites Mal freigeben.
                sale.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return false;
            }

            WarnOnLatePayment(sale);
            SetPaid(sale);
            if (sale.TenantPaymentTerminalId != null && sale.TerminalReceiptJson == null)
            {
                // Ein Terminal-Verkauf, und der Anbieter war schneller als das Geraet. Bezahlt ist er - aber
                // der Beobachter bucht beim Host den Beleg, und dem fehlten jetzt die Kartendaten. Die Meldung
                // wartet auf die Antwort des Terminals oder, falls die ausbleibt, auf die Frist.
                sale.CompletionPendingSince = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                LogEnvironment.LogEvent(
                    $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) was reported paid before the terminal delivered the receipt. The observers are notified once the receipt arrives or the wait period ends.",
                    LogSeverity.Report, LogContext);
                return true;
            }

            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyCompletedAsync(sale, cancellationToken);
            return true;
        }

        /// <summary>
        /// Bucht einen Terminal-Verkauf als bezahlt, mit dem, was das Gerät dazu gemeldet hat.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der Weg der Terminal-Antwort, im Gegensatz zu <see cref="MarkPaidAsync"/> (Webhook). Die Antwort des
        /// Geräts ist das Ende des Vorgangs: was jetzt nicht an Belegangaben da ist, kommt nicht mehr. Darum wird
        /// hier nie zurückgehalten — auch nicht ohne Beleg.
        /// </para>
        /// <para>
        /// War der Webhook schneller, steht der Verkauf schon als bezahlt da und die Meldung aus. Dann wird sie
        /// hier nachgeholt, jetzt mit Beleg.
        /// </para>
        /// </remarks>
        /// <returns>true, wenn dieser Aufruf die Beobachter benachrichtigt hat</returns>
        public async Task<bool> MarkTerminalPaidAsync(TenantSale sale, TContext db, string? chargeId,
            string? customerEmail, TerminalReceipt? receipt, long? tipMinor, CancellationToken cancellationToken)
        {
            TerminalReceiptStore.Store(sale, receipt, tipMinor);
            sale.ProviderChargeId ??= chargeId;
            if (string.IsNullOrWhiteSpace(sale.CustomerEmail) && !string.IsNullOrWhiteSpace(customerEmail))
            {
                sale.CustomerEmail = customerEmail;
            }

            if (IsUnpaid(sale.Status))
            {
                WarnOnLatePayment(sale);
                SetPaid(sale);
                await db.SaveChangesAsync(cancellationToken);
                await notifier.NotifyCompletedAsync(sale, cancellationToken);
                return true;
            }

            if (sale.CompletionPendingSince != null)
            {
                return await ReleasePendingCompletionAsync(sale, db, cancellationToken);
            }

            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        /// <summary>
        /// Holt eine zurückgehaltene Meldung nach — mit Beleg, falls er inzwischen da ist, sonst ohne.
        /// </summary>
        /// <remarks>
        /// <b>Genau einmal</b>, auch wenn Terminal-Antwort, Webhook und Hintergrund-Durchlauf gleichzeitig
        /// kommen, auch über mehrere Instanzen: <see cref="TenantSale.CompletionPendingSince"/> ist ein
        /// Nebenläufigkeits-Merkmal. Wer es nicht als Erster leert, bekommt beim Speichern einen Konflikt und
        /// meldet nicht.
        /// </remarks>
        /// <returns>true, wenn dieser Aufruf die Beobachter benachrichtigt hat</returns>
        public async Task<bool> ReleasePendingCompletionAsync(TenantSale sale, TContext db,
            CancellationToken cancellationToken)
        {
            if (sale.CompletionPendingSince is not { } since || sale.Status == TenantSaleStatus.Pending)
            {
                return false;
            }

            sale.CompletionPendingSince = null;
            sale.Updated = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Ein anderer Weg hat die Meldung schon uebernommen - das ist der Sinn des Merkmals.
                LogEnvironment.LogDebugEvent(
                    $"The completion of sale {sale.TenantSaleId} was already released elsewhere: {ex.Message}",
                    LogSeverity.Report, LogContext);
                return false;
            }

            if (sale.TerminalReceiptJson == null)
            {
                LogEnvironment.LogEvent(
                    $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) is reported to the observers WITHOUT a receipt: none arrived within {(DateTime.UtcNow - since).TotalSeconds:0} seconds of the payment. The receipt booked by the host lacks the card details.",
                    LogSeverity.Warning, LogContext);
            }

            await notifier.NotifyCompletedAsync(sale, cancellationToken);
            return true;
        }

        /// <summary>
        /// Ob auf diesen Verkauf noch eine Zahlung gebucht werden kann - also ob er nie bezahlt wurde.
        /// </summary>
        /// <remarks>
        /// <b>Nicht nur <see cref="TenantSaleStatus.Pending"/>.</b> Ein Endzustand ohne Zahlung ist eine
        /// Aussage ueber die Zahlungsseite, nicht ueber das Geld: bei Stripe meldet ein abgelehnter
        /// Kartenversuch <c>payment_intent.payment_failed</c>, und der Kunde zahlt auf DERSELBEN Seite mit
        /// einer anderen Karte. Ein Abbruch kann eine Zahlung ueberholen, die schon unterwegs war. Kommt das
        /// Geld danach an, ist es da - es zu ignorieren hiesse, eine Zahlung zu verschlucken, die niemand
        /// mehr findet.
        /// </remarks>
        private static bool IsUnpaid(TenantSaleStatus status)
            => status is TenantSaleStatus.Pending or TenantSaleStatus.Failed or TenantSaleStatus.Expired
                or TenantSaleStatus.Canceled;

        private static void WarnOnLatePayment(TenantSale sale)
        {
            if (sale.Status == TenantSaleStatus.Pending)
            {
                return;
            }

            // Abgebrochen heisst: der Host wurde ausdruecklich informiert, dass hier nichts mehr bezahlt wird,
            // und hat womoeglich anders kassiert. Das braucht einen Menschen - darum ein Fehler und keine
            // Warnung. Bei Failed ist es der gewoehnliche zweite Kartenversuch.
            LogEnvironment.LogEvent(
                $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}') was '{sale.Status}' and has now been paid after all. It is booked as paid."
                + (sale.Status == TenantSaleStatus.Canceled ? " The sale had been cancelled — check whether the order was paid a second time elsewhere and refund one of the payments." : string.Empty),
                sale.Status switch
                {
                    TenantSaleStatus.Canceled => LogSeverity.Error,
                    TenantSaleStatus.Expired => LogSeverity.Warning,
                    _ => LogSeverity.Report
                }, LogContext);
        }

        private static void SetPaid(TenantSale sale)
        {
            sale.Status = TenantSaleStatus.Paid;
            sale.PaidUtc = DateTime.UtcNow;
            // Die Zahlungsseite ist verbraucht. Stehen zu lassen hiesse, dem Kunden einen Link zu zeigen,
            // der ihn ein zweites Mal zahlen lassen koennte.
            sale.CheckoutUrl = null;
            sale.Updated = DateTime.UtcNow;
        }

        /// <summary>
        /// Bringt einen UNBEZAHLTEN Verkauf in einen Endzustand (abgebrochen, fehlgeschlagen, abgelaufen).
        /// </summary>
        /// <remarks>
        /// Ein bezahlter bleibt unberührt — Meldungen überholen sich, und eine verspätete Absage darf eine
        /// Zahlung nicht zurücknehmen.
        /// </remarks>
        /// <param name="sale">die Verkaufszeile, verfolgt in <paramref name="db"/></param>
        /// <param name="db">der Kontext, in dem gebucht wird</param>
        /// <param name="status">der Endzustand</param>
        /// <param name="cancellationToken">bricht die Verarbeitung ab</param>
        /// <param name="providerSessionId">
        /// die Zahlungsseite, von der die Meldung handelt, falls sie sie nennt. Ist inzwischen eine NEUE Seite
        /// ausgestellt (nach Ablauf oder Abbruch), betrifft die Absage der alten den Verkauf nicht mehr - sie
        /// wuerde sonst eine gueltige Seite fuer tot erklaeren. Typisch: der Host bricht ab, stellt sofort neu
        /// aus, und Sekunden spaeter kommt das <c>expired</c> der alten Seite.
        /// </param>
        public async Task MoveToAsync(TenantSale sale, TContext db, TenantSaleStatus status,
            CancellationToken cancellationToken, string? providerSessionId = null)
        {
            if (sale.Status != TenantSaleStatus.Pending)
            {
                return;
            }

            if (!string.IsNullOrEmpty(providerSessionId) && !string.IsNullOrEmpty(sale.ProviderSessionId)
                && !string.Equals(providerSessionId, sale.ProviderSessionId, StringComparison.Ordinal))
            {
                LogEnvironment.LogEvent(
                    $"A '{status}' notification for payment page {providerSessionId} reached sale {sale.TenantSaleId}, which has since been given page {sale.ProviderSessionId}. It is ignored — the current page is still valid.",
                    LogSeverity.Report, LogContext);
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
