using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Die Buchfuehrung eines Endkunden-Verkaufs - alles ausser den zwei Stellen, an denen ein
    /// Zahlungsanbieter beruehrt wird.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hier steht, was bei jedem Anbieter gleich ist und wo es wehtut, wenn es auseinanderlaeuft: die
    /// Eindeutigkeit ueber <c>(Mandant, Referenz)</c>, das EINFRIEREN der Provision beim Anlegen, die
    /// Ableitung des Status aus der Summe aller Erstattungen, das Rennen zweier gleichzeitiger Klicks am
    /// eindeutigen Index und die Regel, dass eine unbezahlte Zahlungsseite neu ausgestellt wird, eine
    /// bezahlte aber unberuehrt zurueckkommt.
    /// </para>
    /// <para>
    /// Der Anbieter steuert nur <see cref="CreateCheckoutAsync"/>, <see cref="CreateRefundAsync"/> und
    /// <see cref="CancelCheckoutAsync"/> bei. Alle drei sollen ihre eigenen Fehler in eine <see cref="TenantPaymentException"/> mit
    /// <see cref="PaymentErrorCodes.ProviderError"/> uebersetzen - der Aufrufer soll nicht wissen muessen,
    /// welches SDK dahinter liegt.
    /// </para>
    /// </remarks>
    public abstract class TenantSaleServiceBase<TContext> : ITenantSaleService
        where TContext : DbContext, IPaymentsContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly PaymentsRuntime runtime;
        private readonly TenantSaleNotifier notifier;

        /// <summary>
        /// Bucht eine Zahlung oder einen Abbruch, der beim Abbrechen festgestellt wird - mit denselben Regeln
        /// wie ein Webhook (einmal freigeben, eine Zahlung nie zuruecknehmen).
        /// </summary>
        private readonly TenantSaleWebhookSink<TContext> sink;

        protected TenantSaleServiceBase(IDbContextFactory<TContext> dbFactory, PaymentsRuntime runtime,
            TenantSaleNotifier notifier)
        {
            this.dbFactory = dbFactory;
            this.runtime = runtime;
            this.notifier = notifier;
            sink = new TenantSaleWebhookSink<TContext>(dbFactory, notifier);
            accessRouter = new ProviderAccessRouter<TContext>(dbFactory);
        }

        private readonly ProviderAccessRouter<TContext> accessRouter;

        /// <summary>Die gemeinsame Klammer: Hauptschalter, Feature-Gate, Verkaufsfaehigkeit, Einstellungen.</summary>
        protected PaymentsRuntime Runtime => runtime;

        /// <summary>
        /// Ob diese Anbindung mit einem eigenen Zugang des Mandanten (<see cref="TenantProviderAccess"/>) umgehen
        /// kann. Solange nicht, wird ein Verkauf eines Mandanten mit eigenem Zugang ABGELEHNT - nicht ueber den
        /// Plattform-Zugang gefuehrt.
        /// </summary>
        /// <remarks>
        /// Wer <c>true</c> meldet, holt sich in jedem Anbieter-Schritt den Zugang des Verkaufs ueber
        /// <see cref="AccessForSaleAsync"/> und bekommt in <see cref="CreateCheckoutAsync"/> womoeglich kein
        /// Plattform-Konto.
        /// </remarks>
        protected virtual bool SupportsOwnAccess => false;

        /// <summary>
        /// Ob diese Anbindung NUR mit einem eigenen Zugang des Mandanten arbeitet - bei einem Anbieter, bei dem die
        /// Plattform kein Konto fuehrt (SumUp). Ohne eigenen Zugang wird dann abgelehnt.
        /// </summary>
        protected virtual bool RequiresOwnAccess => false;

        /// <summary>Der Zugang, ueber den dieser Verkauf laeuft - eingefroren beim Anlegen.</summary>
        protected Task<ProviderAccessSelection> AccessForSaleAsync(TenantSale sale, CancellationToken cancellationToken)
            => accessRouter.ForSaleAsync(sale, cancellationToken);

        /// <summary>
        /// Der Name dieses Anbieters - <c>stripe</c>, <c>payrexx</c>, <c>wallee</c>. Er wird beim Anlegen
        /// auf die Verkaufszeile geschrieben und entscheidet spaeter, wer die Erstattung ausfuehrt.
        /// </summary>
        /// <remarks>
        /// Abstrakt und nicht mit einer Vorbelegung: ein Anbieter, der vergisst sich zu benennen, wuerde
        /// Zeilen ohne Kennung hinterlassen, und die landen nach einem Anbieterwechsel beim falschen.
        /// Das soll der Compiler verhindern, nicht ein aufmerksamer Leser.
        /// </remarks>
        protected abstract string ProviderKey { get; }

        /// <summary>
        /// Stellt beim Anbieter die gehostete Zahlungsseite aus.
        /// </summary>
        /// <param name="sale">der bereits gebuchte Verkauf - Betrag, Waehrung und Provision stehen fest</param>
        /// <param name="request">der urspruengliche Auftrag, wegen der Rueckkehr-Adressen</param>
        /// <param name="account">
        /// das Plattform-Konto des Mandanten, auf das gezahlt wird. <b>Leer</b>, wenn der Verkauf ueber einen
        /// eigenen Zugang laeuft und der Mandant kein Plattform-Konto hat - das kommt nur bei Anbindungen vor,
        /// die <see cref="SupportsOwnAccess"/> melden; der Zugang steht dann ueber <see cref="AccessForSaleAsync"/>.
        /// </param>
        /// <param name="attempt">
        /// 0 beim ersten Anlauf, 1 bei JEDEM weiteren. Fuer einen Idempotenz-Schluessel reicht das nicht: der
        /// zweite und der dritte Anlauf (abgelaufen, neu ausgestellt, abgebrochen, wieder neu) truegen
        /// denselben Schluessel und bekaemen beim Anbieter die TOTE Seite zurueck. Unterscheiden laesst sie
        /// sich an <see cref="TenantSale.ProviderSessionId"/> - beim Aufruf steht dort noch die VORIGE Seite.
        /// </param>
        protected abstract Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount? account, int attempt, CancellationToken cancellationToken);

        /// <summary>
        /// Loest die Erstattung beim Anbieter aus. Die Betragspruefungen sind bereits gelaufen.
        /// </summary>
        /// <param name="refundApplicationFee">
        /// ob die Provision anteilig mit zurueckgeht. Kann der Anbieter das nicht, MUSS er das melden statt
        /// es stillschweigend zu uebergehen - sonst zahlt der Mandant fuer das Stornieren.
        /// </param>
        /// <param name="alreadyRefundedMinor">bisher erstattete Summe, fuer den Idempotenz-Schluessel</param>
        protected abstract Task<ProviderRefund> CreateRefundAsync(TenantSale sale, long amountMinor,
            string? reason, bool refundApplicationFee, string accountId, long alreadyRefundedMinor,
            CancellationToken cancellationToken);

        /// <summary>
        /// Macht die Zahlungsseite <see cref="TenantSale.ProviderSessionId"/> beim Anbieter ungueltig - oder
        /// stellt fest, warum das nicht geht.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Der Verkauf steht auf <see cref="TenantSaleStatus.Pending"/> und hat eine Seite. Gebucht wird hier
        /// nichts; das tut die Basis mit dem, was zurueckkommt.
        /// </para>
        /// <para>
        /// <b>Nie raten.</b> Laesst sich die Seite nicht schliessen, muss der Anbieter gefragt werden, wie sie
        /// steht. <see cref="ProviderCancelOutcome.Canceled"/> nur, wenn sicher ist, dass auf ihr nicht mehr
        /// bezahlt werden kann - der Aufrufer kassiert daraufhin womoeglich anders.
        /// </para>
        /// <para>
        /// Was nur dieser Anbieter kennt (Stripes PaymentIntent etwa), darf direkt auf die Zeile geschrieben
        /// werden: sie ist verfolgt und wird mit der Buchung gespeichert.
        /// </para>
        /// </remarks>
        protected abstract Task<ProviderCancel> CancelCheckoutAsync(TenantSale sale, CancellationToken cancellationToken);

        /// <inheritdoc />
        public async Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();
            await runtime.EnsureFeatureAsync(request.TenantId, cancellationToken);

            var options = runtime.Options;
            var currency = (string.IsNullOrWhiteSpace(request.Currency) ? options.DefaultCurrency : request.Currency).Trim().ToUpperInvariant();
            var amountMinor = CurrencyMinorUnits.ToMinor(request.Amount, currency);
            if (amountMinor <= 0 || string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(request.ExternalReference))
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidAmount,
                    $"A sale needs a positive amount, a currency and an external reference (got {request.Amount} {currency}, reference '{request.ExternalReference}').");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var account = await db.TenantPaymentAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == request.TenantId, cancellationToken);

            var reference = request.ExternalReference.Trim();
            var sale = await db.TenantSales.Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference, cancellationToken);

            // Ein neuer Verkauf nimmt den Zugang, der jetzt gilt; ein bestehender bleibt bei seinem - auch ein
            // neuer Anlauf derselben Bestellung laeuft auf dem Konto, auf dem sie angelegt wurde.
            var access = sale == null
                ? await accessRouter.ForNewAsync(request.TenantId, ProviderKey, cancellationToken)
                : await accessRouter.ForSaleAsync(sale, cancellationToken);
            ProviderAccessRouter<TContext>.EnsureSupported(access, SupportsOwnAccess, ProviderKey,
                $"Sale '{reference}' of tenant {request.TenantId}", RequiresOwnAccess);
            if (!access.IsOwnAccess)
            {
                // Checked before EVERY sale, not just at onboarding: the provider can restrict a live account days
                // later when it asks for further documents. A tenant's own access is the tenant's own business -
                // the platform account is neither needed for it nor asked about.
                runtime.EnsureCanSell(account);
            }

            if (sale == null)
            {
                sale = new TenantSale
                {
                    TenantId = request.TenantId,
                    ExternalReference = reference,
                    Description = Trim(request.Description, 256) ?? string.Empty,
                    AmountMinor = amountMinor,
                    Currency = currency,
                    // Frozen here and never recomputed: a later change to the rate must not rewrite what this
                    // sale actually cost.
                    ApplicationFeeMinor = ApplicationFeeMath.Calculate(options.ApplicationFee, amountMinor, currency),
                    Status = TenantSaleStatus.Pending,
                    // Eingefroren wie die Provision daneben: nach einem Anbieterwechsel muss diese Zeile
                    // noch sagen koennen, WER sie abgewickelt hat - sonst geht die Erstattung an den
                    // neuen Anbieter und trifft dort nichts.
                    Provider = ProviderKey,
                    // Eingefroren aus demselben Grund: nach einem Kontowechsel wird dieser Verkauf auf dem Konto
                    // erstattet, auf dem er bezahlt wurde.
                    TenantProviderAccessId = access.AccessId,
                    ProviderAccountId = access.IsOwnAccess ? access.ProviderAccountId : account!.ProviderAccountId,
                    CustomerEmail = Trim(request.CustomerEmail, 256),
                    MetadataJson = request.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(request.Metadata) : null,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };
                db.TenantSales.Add(sale);

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                {
                    // The unique index on (tenant, reference) just caught a double submit. Re-read and continue
                    // with the row that won — returning the same sale is the whole point of the reference.
                    LogEnvironment.LogEvent(
                        $"Concurrent attempt to record sale '{reference}' for tenant {request.TenantId}; the existing sale is reused: {ex.OutlineException()}",
                        LogSeverity.Warning, "StripeConnect");
                    db.Entry(sale).State = EntityState.Detached;
                    var winner = await db.TenantSales.Include(s => s.Refunds)
                        .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference, cancellationToken);
                    if (winner == null)
                    {
                        // Not the unique index after all — whatever went wrong belongs to the caller, unaltered.
                        throw;
                    }

                    return await EnsureCheckoutAsync(db, winner, account, request, cancellationToken, wasExisting: true);
                }

                return await EnsureCheckoutAsync(db, sale, account, request, cancellationToken, wasExisting: false);
            }

            // The reference is already known. Paid or refunded sales are handed back untouched — recording the
            // same order twice must never produce a second payment.
            if (sale.Status is TenantSaleStatus.Paid or TenantSaleStatus.Refunded or TenantSaleStatus.PartiallyRefunded)
            {
                return ToResult(sale, wasExisting: true);
            }

            // Pending, expired, failed or cancelled: the order still has not been paid, so the tenant gets a
            // usable payment page again. That is a deliberate second ATTEMPT at the same sale, not a second sale
            // — amount and frozen fee stay as they were.
            return await EnsureCheckoutAsync(db, sale, account, request, cancellationToken, wasExisting: true);
        }

        /// <inheritdoc />
        public async Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.AsNoTracking().Include(s => s.Refunds)
                           .FirstOrDefaultAsync(s => s.TenantSaleId == tenantSaleId, cancellationToken)
                       ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} not found.");
            return ToResult(sale, wasExisting: true);
        }

        /// <inheritdoc />
        public async Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference, CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var reference = externalReference?.Trim() ?? string.Empty;
            var sale = await db.TenantSales.AsNoTracking().Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ExternalReference == reference, cancellationToken);
            return sale == null ? null : ToResult(sale, wasExisting: true);
        }

        /// <inheritdoc />
        public async Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason, bool? refundApplicationFee = null, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.Include(s => s.Refunds)
                           .FirstOrDefaultAsync(s => s.TenantSaleId == tenantSaleId, cancellationToken)
                       ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} not found.");

            if (sale.Status is not (TenantSaleStatus.Paid or TenantSaleStatus.PartiallyRefunded))
            {
                throw new TenantPaymentException(PaymentErrorCodes.SaleNotRefundable,
                    $"Sale {tenantSaleId} is '{sale.Status}' and was never paid.");
            }

            if (string.IsNullOrEmpty(sale.ProviderChargeId))
            {
                throw new TenantPaymentException(PaymentErrorCodes.MissingCharge,
                    $"Sale {tenantSaleId} has no charge on record; the refund cannot be addressed.");
            }

            var alreadyRefunded = sale.Refunds.Sum(r => r.AmountMinor);
            var remaining = sale.AmountMinor - alreadyRefunded;
            var amount = amountMinor ?? remaining;
            if (amount <= 0 || amount > remaining)
            {
                throw new TenantPaymentException(PaymentErrorCodes.RefundExceedsAmount,
                    $"Sale {tenantSaleId} has {remaining} of {sale.AmountMinor} left; {amount} cannot be refunded.");
            }

            var withFee = refundApplicationFee ?? runtime.Options.RefundApplicationFeeByDefault;
            var access = await accessRouter.ForSaleAsync(sale, cancellationToken);
            ProviderAccessRouter<TContext>.EnsureSupported(access, SupportsOwnAccess, ProviderKey,
                $"Refund of sale {sale.TenantSaleId}", RequiresOwnAccess);
            // Bei einem eigenen Zugang steht die Kennung auf der Zeile oder nirgends: das Plattform-Konto als
            // Rueckfall waere ein Konto, auf dem diese Zahlung nie lag.
            var accountId = sale.ProviderAccountId
                            ?? (access.IsOwnAccess ? access.ProviderAccountId : (await db.TenantPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == sale.TenantId, cancellationToken))?.ProviderAccountId)
                            ?? throw new TenantPaymentException(PaymentErrorCodes.NoAccount, $"Tenant {sale.TenantId} has no payout account.");

            var refund = await CreateRefundAsync(sale, amount, reason, withFee, accountId, alreadyRefunded, cancellationToken);

            // How much of the commission actually went back. The provider returns it proportionally to the
            // refunded share (and in full for a full refund); without recording it here the commission statement
            // would still show a fee that no longer exists.
            var feeAlreadyBack = sale.Refunds.Sum(r => r.ApplicationFeeRefundedMinor);
            var feeBack = withFee
                ? Math.Clamp(ApplicationFeeMath.ProportionalRefund(sale.ApplicationFeeMinor, amount, sale.AmountMinor), 0, Math.Max(0, sale.ApplicationFeeMinor - feeAlreadyBack))
                : 0;

            var row = new TenantSaleRefund
            {
                TenantSaleId = sale.TenantSaleId,
                AmountMinor = amount,
                ApplicationFeeRefundedMinor = feeBack,
                ProviderRefundId = refund.RefundId,
                Reason = Trim(reason, 512),
                Status = refund.Status,
                Created = DateTime.UtcNow
            };
            sale.Refunds.Add(row);
            sale.Status = DeriveStatus(sale.AmountMinor, alreadyRefunded + amount);
            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            if (!withFee)
            {
                // Not an error, but worth finding again: the platform keeps its cut of a reversed deal, so the
                // tenant pays for cancelling. That has to be a decision someone can point at.
                LogEnvironment.LogEvent(
                    $"Refund {refund.RefundId} on sale {sale.TenantSaleId} (tenant {sale.TenantId}) was booked WITHOUT returning the platform commission of {sale.ApplicationFeeMinor} {sale.Currency}.",
                    LogSeverity.Warning, "StripeConnect");
            }

            // The provider will also deliver charge.refunded for this. The webhook mirrors only refunds it does
            // not already know, so the observers are called exactly once — here, because this is where the row
            // came into existence.
            await notifier.NotifyRefundedAsync(sale, row, cancellationToken);

            return new RefundResult
            {
                TenantSaleRefundId = row.TenantSaleRefundId,
                TenantSaleId = sale.TenantSaleId,
                AmountMinor = amount,
                ApplicationFeeRefundedMinor = feeBack,
                ProviderRefundId = refund.RefundId,
                Status = refund.Status,
                SaleStatus = sale.Status
            };
        }

        /// <inheritdoc />
        public async Task<SaleResult> CancelSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            runtime.EnsureEnabled();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var sale = await db.TenantSales.Include(s => s.Refunds)
                           .FirstOrDefaultAsync(s => s.TenantSaleId == tenantSaleId, cancellationToken)
                       ?? throw new TenantPaymentException(PaymentErrorCodes.SaleNotFound, $"Sale {tenantSaleId} not found.");

            if (sale.TenantPaymentTerminalId != null)
            {
                // Die Kennung auf einem Terminal-Verkauf ist ein Geraetevorgang, keine Zahlungsseite. Sie dem
                // Seiten-Abbruch des Anbieters zu geben, trifft im besten Fall nichts.
                throw new InvalidOperationException(
                    $"Sale {tenantSaleId} is a terminal sale; cancel it through ITerminalPaymentService.CancelPaymentAsync.");
            }

            if (sale.Status != TenantSaleStatus.Pending)
            {
                // Bezahlt, erstattet, abgelaufen, gescheitert oder schon abgebrochen: so wie es ist. Ein zweiter
                // Abbruch ist damit harmlos.
                return ToResult(sale, wasExisting: true);
            }

            if (string.IsNullOrEmpty(sale.ProviderSessionId))
            {
                // Die Seite ist nie entstanden (der Anbieter hat beim Anlegen abgelehnt). Es gibt nichts zu
                // schliessen, aber der Verkauf soll trotzdem sagen, dass er aufgegeben wurde.
                await sink.MoveToAsync(sale, db, TenantSaleStatus.Canceled, cancellationToken);
                return ToResult(sale, wasExisting: true);
            }

            ProviderAccessRouter<TContext>.EnsureSupported(await accessRouter.ForSaleAsync(sale, cancellationToken),
                SupportsOwnAccess, ProviderKey, $"Cancelling sale {sale.TenantSaleId}", RequiresOwnAccess);
            var outcome = await CancelCheckoutAsync(sale, cancellationToken);

            // Waehrend der Anbieter gefragt wurde, kann ein Webhook die Zahlung gebucht haben. Dann gilt, was
            // er geschrieben hat - mit dem alten Stand weiterzubuchen, liesse die Beobachter zweimal feuern.
            var stored = await db.TenantSales.AsNoTracking().Where(s => s.TenantSaleId == sale.TenantSaleId)
                .Select(s => s.Status).FirstAsync(cancellationToken);
            if (stored != sale.Status)
            {
                await db.Entry(sale).ReloadAsync(cancellationToken);
                return ToResult(sale, wasExisting: true);
            }

            switch (outcome.Outcome)
            {
                case ProviderCancelOutcome.Canceled:
                    await sink.MoveToAsync(sale, db, TenantSaleStatus.Canceled, cancellationToken);
                    if (sale.Status == TenantSaleStatus.Canceled)
                    {
                        LogEnvironment.LogEvent(
                            $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}') was cancelled; its payment page {sale.ProviderSessionId} at {ProviderKey} no longer accepts a payment.",
                            LogSeverity.Report, TenantSaleWebhookSink<TContext>.LogContext);
                    }

                    break;

                case ProviderCancelOutcome.Paid:
                    // Der Kunde war schneller. Die Zahlung wird hier gebucht und nicht dem Webhook ueberlassen:
                    // der Aufrufer bekommt "bezahlt" zu hoeren und soll in der Datenbank dasselbe finden.
                    // Kommt der Webhook danach, ist es fuer die Senke eine Wiederholung.
                    LogEnvironment.LogEvent(
                        $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}') could not be cancelled: it was paid on {ProviderKey} first. It is booked as paid.",
                        LogSeverity.Warning, TenantSaleWebhookSink<TContext>.LogContext);
                    await sink.MarkPaidAsync(sale, db, outcome.ChargeId, outcome.CustomerEmail, cancellationToken);
                    break;

                case ProviderCancelOutcome.InProgress:
                    // Nichts entscheiden: die Seite ist noch zahlbar, oder eine Zahlung ist unterwegs.
                    LogEnvironment.LogEvent(
                        $"Sale {sale.TenantSaleId} (tenant {sale.TenantId}, reference '{sale.ExternalReference}') could not be cancelled at {ProviderKey}: {outcome.Detail ?? "a payment is in progress"}. It stays pending.",
                        LogSeverity.Warning, TenantSaleWebhookSink<TContext>.LogContext);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown cancel outcome '{outcome.Outcome}' from {ProviderKey}.");
            }

            return ToResult(sale, wasExisting: true);
        }

        private async Task<SaleResult> EnsureCheckoutAsync(TContext db, TenantSale sale, TenantPaymentAccount? account, SaleRequest request, CancellationToken cancellationToken, bool wasExisting)
        {
            if (sale.Status == TenantSaleStatus.Pending && !string.IsNullOrEmpty(sale.CheckoutUrl) && !string.IsNullOrEmpty(sale.ProviderSessionId))
            {
                return ToResult(sale, wasExisting);
            }

            var options = runtime.Options;
            var expiryMinutes = Math.Clamp(options.CheckoutExpiryMinutes, 30, 1440);
            var attempt = sale.Status == TenantSaleStatus.Pending && string.IsNullOrEmpty(sale.ProviderSessionId) ? 0 : 1;

            var checkout = await CreateCheckoutAsync(sale, request, account, attempt, cancellationToken);

            sale.ProviderSessionId = checkout.SessionId;
            sale.CheckoutUrl = checkout.Url;
            sale.Status = TenantSaleStatus.Pending;
            if (sale.TenantProviderAccessId == null && account != null)
            {
                // Nur beim Plattform-Zugang nachziehen. Ein eigener Zugang hat seine Kennung beim Anlegen
                // eingefroren - das Plattform-Konto daneben hat mit diesem Verkauf nichts zu tun.
                sale.ProviderAccountId = account.ProviderAccountId;
            }

            sale.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            return ToResult(sale, wasExisting);
        }

        /// <summary>
        /// Sale status from the refunded total — never from a single refund row.
        /// </summary>
        /// <remarks>
        /// Die EINZIGE Stelle, an der diese Frage beantwortet wird - hier und in
        /// <see cref="TenantSaleWebhookSink{TContext}"/>, wo Erstattungen ankommen, die anderswo ausgeloest
        /// wurden (im Portal des Anbieters etwa). Gaebe es zwei Regeln dafuer, fiele die zweite erst auf,
        /// wenn ein teilerstatteter Verkauf als vollstaendig erstattet dasteht.
        /// <para>
        /// Oeffentlich, damit ein Host, der eine Erstattung selbst verbucht, dieselbe Regel anwenden kann -
        /// nicht mehr, weil ein anderes Assembly sie braeuchte.
        /// </para>
        /// </remarks>
        public static TenantSaleStatus DeriveStatus(long amountMinor, long refundedMinor)
            => refundedMinor <= 0 ? TenantSaleStatus.Paid
                : refundedMinor >= amountMinor ? TenantSaleStatus.Refunded
                : TenantSaleStatus.PartiallyRefunded;

        private static SaleResult ToResult(TenantSale sale, bool wasExisting) => new()
        {
            TenantSaleId = sale.TenantSaleId,
            TenantId = sale.TenantId,
            ExternalReference = sale.ExternalReference,
            Description = sale.Description,
            Status = sale.Status,
            CheckoutUrl = sale.Status == TenantSaleStatus.Pending ? sale.CheckoutUrl : null,
            AmountMinor = sale.AmountMinor,
            ApplicationFeeMinor = sale.ApplicationFeeMinor,
            RefundedMinor = sale.Refunds?.Sum(r => r.AmountMinor) ?? 0,
            Currency = sale.Currency,
            PaidUtc = sale.PaidUtc,
            Created = sale.Created,
            CustomerEmail = sale.CustomerEmail,
            WasExisting = wasExisting
        };

        private static string? Trim(string? value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
    }

    /// <summary>Was der Anbieter zurueckmeldet, nachdem er eine Zahlungsseite ausgestellt hat.</summary>
    /// <param name="SessionId">seine Kennung des Vorgangs - landet auf der Verkaufszeile</param>
    /// <param name="Url">die Adresse, auf die der Endkunde geschickt wird</param>
    public readonly record struct ProviderCheckout(string SessionId, string Url);

    /// <summary>Was der Anbieter zurueckmeldet, nachdem er erstattet hat.</summary>
    /// <param name="RefundId">seine Kennung der Erstattung</param>
    /// <param name="Status">sein Status-Wort, unveraendert uebernommen</param>
    public readonly record struct ProviderRefund(string RefundId, string? Status);

    /// <summary>Was beim Versuch herauskam, eine Zahlungsseite zu schliessen.</summary>
    public enum ProviderCancelOutcome
    {
        /// <summary>Auf der Seite kann nicht mehr bezahlt werden - geschlossen oder schon abgelaufen.</summary>
        Canceled,

        /// <summary>Zu spaet: auf der Seite wurde bezahlt.</summary>
        Paid,

        /// <summary>
        /// Weder noch: eine Zahlung ist unterwegs, oder der Anbieter kann die Seite vor ihrem Ablauf nicht
        /// schliessen. Sie gilt weiter als zahlbar.
        /// </summary>
        InProgress
    }

    /// <summary>Was der Anbieter zum Schliessen einer Zahlungsseite zurueckmeldet.</summary>
    /// <param name="Outcome">das Ergebnis</param>
    /// <param name="ChargeId">bei <see cref="ProviderCancelOutcome.Paid"/>: die Kennung, gegen die erstattet wird, falls bekannt</param>
    /// <param name="CustomerEmail">bei <see cref="ProviderCancelOutcome.Paid"/>: die auf der Seite erfasste Adresse, falls gewuenscht</param>
    /// <param name="Detail">bei <see cref="ProviderCancelOutcome.InProgress"/>: warum, fuers Protokoll</param>
    public readonly record struct ProviderCancel(ProviderCancelOutcome Outcome, string? ChargeId = null,
        string? CustomerEmail = null, string? Detail = null);
}
