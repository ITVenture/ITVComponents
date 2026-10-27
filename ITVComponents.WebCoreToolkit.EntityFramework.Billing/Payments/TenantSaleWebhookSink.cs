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
        /// <remarks>
        /// <b>Ohne Eingrenzung - fuer neue Anbindungen die Ueberladung mit <see cref="SaleSearchScope"/>
        /// verwenden.</b> Diese hier sucht die Kennung ueber ALLE Anbieter und Zugaenge; bei Anbietern mit
        /// fortlaufenden Nummern (Payrexx, wallee) kann dieselbe Zahl bei zweien vorkommen.
        /// </remarks>
        public Task<TenantSale?> FindSaleAsync(TContext db, string? providerSessionId,
            string? externalReference, int? tenantId, CancellationToken cancellationToken)
            => FindSaleAsync(db, null, providerSessionId, externalReference, tenantId, cancellationToken);

        /// <summary>
        /// Sucht den Verkauf, den eine Meldung meint - nur unter den Verkaeufen dieses Anbieters und dieses
        /// Zugangs.
        /// </summary>
        /// <param name="db">der Kontext, in dem gesucht wird</param>
        /// <param name="scope">
        /// Anbieter und Zugang, von dem die Meldung kommt. Die Kennungen von Payrexx und wallee sind
        /// fortlaufende Nummern - je Instanz bzw. Space. Ohne Eingrenzung traefe eine wallee-Meldung zur
        /// Transaktion 123 einen Payrexx-Verkauf mit Zahlungsseite 123, und die Meldung eines eigenen Zugangs
        /// einen Verkauf auf dem Plattform-Konto mit derselben Nummer.
        /// </param>
        /// <param name="providerSessionId">die Kennung, die der Anbieter beim Anlegen vergeben hat</param>
        /// <param name="externalReference">unsere eigene Referenz, als zweiter Versuch</param>
        /// <param name="tenantId">der Mandant, falls die Meldung ihn nennt</param>
        /// <param name="cancellationToken">bricht die Suche ab</param>
        public async Task<TenantSale?> FindSaleAsync(TContext db, SaleSearchScope? scope, string? providerSessionId,
            string? externalReference, int? tenantId, CancellationToken cancellationToken)
        {
            IQueryable<TenantSale> sales = db.TenantSales.Include(s => s.Refunds);
            if (scope is { } within)
            {
                var provider = ProviderAccessRouter<TContext>.Normalize(within.Provider);
                var accessId = within.TenantProviderAccessId;
                // Zeilen ohne Anbieter stammen aus der Zeit vor der Spalte - vom damals einzigen Anbieter.
                sales = sales.Where(s => (s.Provider == provider || s.Provider == null) && s.TenantProviderAccessId == accessId);
            }

            if (!string.IsNullOrWhiteSpace(providerSessionId))
            {
                var bySession = await sales
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
                return await sales
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ExternalReference == externalReference,
                        cancellationToken);
            }

            // Ohne Mandant ist die Referenz nicht eindeutig. Lieber nichts zurueckgeben als den Verkauf
            // eines FREMDEN Mandanten zu bezahlen, nur weil er dieselbe Bestellnummer vergeben hat.
            var candidates = await sales
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
        /// <remarks>
        /// <b>Genau einmal</b>, auch wenn Webhook, Terminal-Antwort und Nachfragen der Kasse gleichzeitig
        /// buchen: der Übergang auf bezahlt ist über <see cref="TenantSale.PaidUtc"/> abgesichert (siehe
        /// <see cref="TrySaveAsync"/>). Wer ihn verliert, entscheidet auf dem Stand des Gewinners neu.
        /// </remarks>
        /// <returns>true, wenn dieser Aufruf den Verkauf freigegeben hat — false bei einer Wiederholung</returns>
        public async Task<bool> MarkPaidAsync(TenantSale sale, TContext db, string? chargeId,
            string? customerEmail, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
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
                    if (!await TrySaveAsync(sale, db, cancellationToken))
                    {
                        if (attempt < MaxBookingAttempts)
                        {
                            continue;
                        }

                        LogGivenUp(sale);
                    }

                    return false;
                }

                WarnOnLatePayment(sale);
                SetPaid(sale);
                // Ein Terminal-Verkauf, und der Anbieter war schneller als das Geraet. Bezahlt ist er - aber der
                // Beobachter bucht beim Host den Beleg, und dem fehlten jetzt die Kartendaten. Die Meldung wartet
                // auf die Antwort des Terminals oder, falls die ausbleibt, auf die Frist.
                var holdBack = sale.TenantPaymentTerminalId != null && sale.TerminalReceiptJson == null;
                if (holdBack)
                {
                    sale.CompletionPendingSince = DateTime.UtcNow;
                }

                if (!await TrySaveAsync(sale, db, cancellationToken))
                {
                    // Ein anderer Weg hat bezahlt. Auf seinem Stand neu entscheiden - dort ist es eine Wiederholung.
                    if (attempt < MaxBookingAttempts)
                    {
                        continue;
                    }

                    LogGivenUp(sale);
                    return false;
                }

                if (holdBack)
                {
                    LogEnvironment.LogEvent(
                        $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) was reported paid before the terminal delivered the receipt. The observers are notified once the receipt arrives or the wait period ends.",
                        LogSeverity.Report, LogContext);
                    return true;
                }

                await notifier.NotifyCompletedAsync(sale, cancellationToken);
                return true;
            }
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
        /// <para>
        /// Fragen zwei Wege gleichzeitig nach (Kasse und SumUp-Rückmeldung, zwei offene Fenster) oder bucht
        /// ein Webhook zur selben Zeit, meldet nur, wer den Übergang auf bezahlt gewinnt (siehe
        /// <see cref="TrySaveAsync"/>). Der Verlierer legt seinen Beleg auf den Stand des Gewinners - und gibt
        /// dessen zurückgehaltene Meldung frei, falls der Gewinner ein Webhook ohne Beleg war.
        /// </para>
        /// </remarks>
        /// <returns>true, wenn dieser Aufruf die Beobachter benachrichtigt hat</returns>
        public async Task<bool> MarkTerminalPaidAsync(TenantSale sale, TContext db, string? chargeId,
            string? customerEmail, TerminalReceipt? receipt, long? tipMinor, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                // Bei jedem Durchgang neu: ein verlorener Wettlauf laedt die Zeile neu und verwirft dabei, was
                // dieser Weg schon daraufgelegt hatte.
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
                    if (!await TrySaveAsync(sale, db, cancellationToken))
                    {
                        if (attempt < MaxBookingAttempts)
                        {
                            continue;
                        }

                        LogGivenUp(sale);
                        return false;
                    }

                    await notifier.NotifyCompletedAsync(sale, cancellationToken);
                    return true;
                }

                if (sale.CompletionPendingSince != null)
                {
                    return await ReleasePendingCompletionAsync(sale, db, cancellationToken);
                }

                sale.Updated = DateTime.UtcNow;
                if (!await TrySaveAsync(sale, db, cancellationToken))
                {
                    if (attempt < MaxBookingAttempts)
                    {
                        continue;
                    }

                    LogGivenUp(sale);
                }

                return false;
            }
        }

        /// <summary>
        /// Speichert die Verkaufszeile — es sei denn, ein anderer Weg hat sie inzwischen bezahlt oder eine
        /// zurückgehaltene Meldung übernommen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="TenantSale.PaidUtc"/> und <see cref="TenantSale.CompletionPendingSince"/> sind
        /// Nebenläufigkeits-Merkmale. Der Übergang auf bezahlt setzt <c>PaidUtc</c> genau einmal; wer die Zeile
        /// vorher gelesen hat, scheitert danach beim Speichern. Ohne das meldeten zwei gleichzeitige Wege,
        /// die beide noch <see cref="TenantSaleStatus.Pending"/> gesehen haben, beide.
        /// </para>
        /// <para>
        /// Scheitert das Speichern daran, wird die Zeile neu geladen: der Aufrufer sieht den Stand des
        /// Gewinners und entscheidet darauf neu, statt auf einem Stand weiterzubuchen, den es nicht mehr gibt.
        /// Seine eigenen, noch nicht gespeicherten Änderungen sind dabei verworfen — er muss sie wiederholen.
        /// </para>
        /// </remarks>
        /// <returns>true, wenn gespeichert wurde; false, wenn die Zeile stattdessen neu geladen wurde</returns>
        internal async Task<bool> TrySaveAsync(TenantSale sale, TContext db, CancellationToken cancellationToken)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Der erwartete Ausgang eines Wettlaufs, kein Fehler - der Aufrufer entscheidet neu.
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) was changed by another path while this one was booking it, typically a payment notification and a terminal answer at the same time. It is re-read and decided again on the current state: {ex.Message}",
                    LogSeverity.Report, LogContext);
                await db.Entry(sale).ReloadAsync(cancellationToken);
                return false;
            }
        }

        /// <summary>
        /// Wie oft ein Weg nach einem verlorenen Wettlauf auf dem frischen Stand neu entscheidet.
        /// </summary>
        /// <remarks>
        /// Der Übergang auf bezahlt geschieht genau einmal, ein zweiter Durchgang sieht also einen bezahlten
        /// Verkauf. Der dritte deckt ab, dass dazwischen noch eine zurückgehaltene Meldung freigegeben wurde.
        /// </remarks>
        private const int MaxBookingAttempts = 3;

        private static void LogGivenUp(TenantSale sale)
            => LogEnvironment.LogEvent(
                $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) kept changing under this booking for {MaxBookingAttempts} attempts; what this notification carried (charge id, e-mail, receipt) was NOT stored. The status itself is as the other paths left it. A repeated delivery of the notification stores it.",
                LogSeverity.Error, LogContext);

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
            for (var attempt = 1; ; attempt++)
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
                // Wurde der Verkauf inzwischen bezahlt, scheitert das Speichern, und der frische Stand ist nicht
                // mehr Pending: die Absage hat dann nichts mehr zu sagen. Ohne diese Pruefung ueberschrieb eine
                // gleichzeitige Absage eine gerade gebuchte Zahlung.
                if (await TrySaveAsync(sale, db, cancellationToken))
                {
                    return;
                }

                if (attempt >= MaxBookingAttempts)
                {
                    LogEnvironment.LogEvent(
                        $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}) could not be moved to '{status}': it kept changing under this booking for {MaxBookingAttempts} attempts. It stays '{sale.Status}'.",
                        LogSeverity.Error, LogContext);
                    return;
                }
            }
        }

        /// <summary>
        /// Schliesst einen offenen Terminal-Verkauf <b>von Hand</b> ab — als bezahlt oder als nicht bezahlt.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der letzte Ausweg, wenn weder Gerät noch Anbieter das Ergebnis liefern können (Payrexx ohne Antwort auf den
        /// Start, wallee LTI nach einer neueren Zahlung, Gerät inzwischen bei einem anderen Anbieter). Ein Mensch hat
        /// am Gerät oder beim Anbieter nachgesehen und entscheidet.
        /// </para>
        /// <para>
        /// Bezahlt geht durch denselben Übergang wie eine Terminal-Antwort: die Beobachter werden genau einmal
        /// benachrichtigt (ohne Beleg). Nicht bezahlt wird <see cref="TenantSaleStatus.Canceled"/> - kommt danach doch
        /// noch eine Zahlung herein, wird sie gebucht und als Fehler gemeldet, wie bei jedem abgebrochenen Verkauf.
        /// Hat inzwischen ein anderer Weg entschieden, gilt dessen Entscheidung; der Aufrufer sieht sie am Ergebnis.
        /// </para>
        /// <para>
        /// Es gibt dafür keine Spalte; die Spur ist das Protokoll - wer, wann, warum, mit welcher Kennung.
        /// </para>
        /// </remarks>
        /// <param name="sale">der Verkauf, verfolgt in <paramref name="db"/></param>
        /// <param name="db">der Kontext, in dem gebucht wird</param>
        /// <param name="paid">true = als bezahlt abschliessen, false = als nicht bezahlt</param>
        /// <param name="reason">warum - Pflicht</param>
        /// <param name="actor">wer entschieden hat</param>
        /// <param name="providerReference">
        /// die Kennung der Zahlung beim Anbieter, falls bekannt. Wird bei bezahlt als Belastung gespeichert - ohne sie
        /// lässt sich der Verkauf später nicht über den Anbieter erstatten.
        /// </param>
        /// <param name="cancellationToken">bricht die Verarbeitung ab</param>
        /// <returns>der Zustand des Verkaufs danach</returns>
        public async Task<TenantSaleStatus> SettleTerminalSaleByHandAsync(TenantSale sale, TContext db, bool paid,
            string reason, string actor, string? providerReference, CancellationToken cancellationToken)
        {
            if (sale.TenantPaymentTerminalId == null)
            {
                throw new InvalidOperationException(
                    $"Sale {sale.TenantSaleId} was not made at a terminal; only terminal sales are settled by hand.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new TenantPaymentException(PaymentErrorCodes.ReasonRequired,
                    $"Settling sale {sale.TenantSaleId} by hand needs a reason.");
            }

            var before = sale.Status;
            if (before == TenantSaleStatus.Pending)
            {
                var reference = string.IsNullOrWhiteSpace(providerReference) ? null : providerReference.Trim();
                if (paid)
                {
                    await MarkTerminalPaidAsync(sale, db, reference, null, null, null, cancellationToken);
                }
                else
                {
                    await MoveToAsync(sale, db, TenantSaleStatus.Canceled, cancellationToken);
                }
            }

            var wanted = paid ? TenantSaleStatus.Paid : TenantSaleStatus.Canceled;
            LogEnvironment.LogEvent(
                sale.Status == wanted && before == TenantSaleStatus.Pending
                    ? $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}', {sale.AmountMinor} {sale.Currency}) was settled BY HAND as '{sale.Status}' by '{actor}'. Reason: {reason.Trim()}. Provider reference: {providerReference ?? "none"}."
                    : $"'{actor}' tried to settle terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) by hand as '{wanted}', but it is '{sale.Status}' (was '{before}' when asked) - another path decided first. Nothing was changed by hand. Reason given: {reason.Trim()}.",
                LogSeverity.Warning, LogContext);
            return sale.Status;
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
