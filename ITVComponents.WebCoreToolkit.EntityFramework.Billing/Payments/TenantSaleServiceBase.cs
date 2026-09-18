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
    /// Der Anbieter steuert nur <see cref="CreateCheckoutAsync"/> und <see cref="CreateRefundAsync"/> bei.
    /// Beide sollen ihre eigenen Fehler in eine <see cref="TenantPaymentException"/> mit
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

        protected TenantSaleServiceBase(IDbContextFactory<TContext> dbFactory, PaymentsRuntime runtime,
            TenantSaleNotifier notifier)
        {
            this.dbFactory = dbFactory;
            this.runtime = runtime;
            this.notifier = notifier;
        }

        /// <summary>Die gemeinsame Klammer: Hauptschalter, Feature-Gate, Verkaufsfaehigkeit, Einstellungen.</summary>
        protected PaymentsRuntime Runtime => runtime;

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
        /// <param name="account">das Konto des Mandanten, auf das gezahlt wird</param>
        /// <param name="attempt">
        /// 0 beim ersten Anlauf, 1 bei einem weiteren. Gehoert in den Idempotenz-Schluessel: ein wiederholter
        /// Klick soll dieselbe Seite bekommen, ein bewusster zweiter Anlauf nach Ablauf eine neue.
        /// </param>
        protected abstract Task<ProviderCheckout> CreateCheckoutAsync(TenantSale sale, SaleRequest request,
            TenantPaymentAccount account, int attempt, CancellationToken cancellationToken);

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
            // Checked before EVERY sale, not just at onboarding: the provider can restrict a live account days
            // later when it asks for further documents.
            runtime.EnsureCanSell(account);

            var reference = request.ExternalReference.Trim();
            var sale = await db.TenantSales.Include(s => s.Refunds)
                .FirstOrDefaultAsync(s => s.TenantId == request.TenantId && s.ExternalReference == reference, cancellationToken);

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
                    ProviderAccountId = account!.ProviderAccountId,
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

                    return await EnsureCheckoutAsync(db, winner, account!, request, cancellationToken, wasExisting: true);
                }

                return await EnsureCheckoutAsync(db, sale, account!, request, cancellationToken, wasExisting: false);
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
            return await EnsureCheckoutAsync(db, sale, account!, request, cancellationToken, wasExisting: true);
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
            var accountId = sale.ProviderAccountId
                            ?? (await db.TenantPaymentAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == sale.TenantId, cancellationToken))?.ProviderAccountId
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

        private async Task<SaleResult> EnsureCheckoutAsync(TContext db, TenantSale sale, TenantPaymentAccount account, SaleRequest request, CancellationToken cancellationToken, bool wasExisting)
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
            sale.ProviderAccountId = account.ProviderAccountId;
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
}
