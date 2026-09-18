using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Impl
{
    /// <summary>
    /// Kassieren an einem Payrexx-Terminal über deren Kassen-Schnittstelle (ECR).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das Gerät wird über seine <b>Seriennummer</b> angesprochen und muss vorher mit dem Konto
    /// gekoppelt sein (<c>/pair</c>, am Gerät mit einem Einmalcode bestätigt). Payrexx spricht dann mit
    /// dem Terminal — es gibt <b>keinen</b> lokalen Weg, auch nicht über einen Agenten.
    /// </para>
    /// <para>
    /// <b>Die Provision reist nicht mit</b>, wie schon bei den Online-Verkäufen: Payrexx kennt kein Feld
    /// je Vorgang dafür. Der Betrag auf der Zeile ist eine Buchgrösse, die Marge entsteht aus den
    /// Konditionen der Plattform.
    /// </para>
    /// <para>
    /// <b>Erstattungen kann nicht jedes Gerät.</b> Für NexGo-Terminals ist <c>payment/refund</c> laut
    /// Payrexx nicht unterstützt — dort geht nur <c>void</c>, und nur solange der Tagesabschluss nicht
    /// gelaufen ist.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Gerät gelaufen.</b>
    /// </para>
    /// </remarks>
    public class PayrexxTerminalPaymentService<TContext> : TerminalPaymentServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Der Name, unter dem dieser Weg an einem Gerät eingetragen wird.</summary>
        public const string Key = "payrexx";

        private readonly PayrexxEcrClient ecr;

        /// <summary>Initializes a new instance of the <see cref="PayrexxTerminalPaymentService{TContext}"/> class.</summary>
        public PayrexxTerminalPaymentService(IDbContextFactory<TContext> dbFactory, PayrexxEcrClient ecr,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            TenantSaleWebhookSink<TContext> sink)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()), sink)
        {
            this.ecr = ecr;
        }

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            if (sale.ApplicationFeeMinor > 0)
            {
                LogEnvironment.LogEvent(
                    $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) carries a commission of {sale.ApplicationFeeMinor} {sale.Currency}. Payrexx has no per-transaction commission field — this amount is a local booking figure and is not withheld here.",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
            }

            var payload = new Dictionary<string, object?>
            {
                // Rappen, nicht Franken. Wer hier Franken schickt, kassiert ein Hundertstel - und das
                // Geraet zeigt den Betrag anstandslos an.
                ["amount"] = sale.AmountMinor,
                ["currency"] = sale.Currency.ToUpperInvariant(),
                ["transactionDescriptor"] = sale.Description,
                ["paymentReference"] = sale.ExternalReference,
                // Den Beleg druckt der Kassen-Agent, nicht das Terminal: er hat den Drucker, und der Beleg
                // gehoert zum Kassenbon. Die Zeilen dafuer kommen in der Antwort mit.
                ["printSlip"] = false
            };

            try
            {
                var payment = await ecr.SendAsync(HttpMethod.Post, $"ecr/{Escape(terminal)}/payment", payload,
                    cancellationToken);
                return Translate(payment, command.OperationId);
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Payrexx did not confirm the terminal payment for sale {sale.TenantSaleId} on terminal {terminal.ProviderTerminalId}. The sale stays open — it is NOT known whether the card was charged: {ex.OutlineException()}",
                    LogSeverity.Error, PayrexxApiClient.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = command.OperationId,
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = ex.Message
                };
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sale.ProviderSessionId))
            {
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            try
            {
                var payment = await ecr.SendAsync(HttpMethod.Get,
                    $"ecr/{Escape(terminal)}/payment/{Uri.EscapeDataString(sale.ProviderSessionId)}", null,
                    cancellationToken);
                return Translate(payment, operationId);
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not ask Payrexx about payment {sale.ProviderSessionId} on terminal {terminal.ProviderTerminalId} (sale {sale.TenantSaleId}): {ex.OutlineException()}",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = ex.Message
                };
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sale.ProviderSessionId))
            {
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            try
            {
                // 'cancel' bricht einen LAUFENDEN Vorgang ab. Fuer einen abgeschlossenen waere es 'void' -
                // das ist eine andere Entscheidung und gehoert nicht in diesen Aufruf, weil hier niemand
                // sicher weiss, ob die Karte schon belastet wurde.
                var payment = await ecr.SendAsync(HttpMethod.Post,
                    $"ecr/{Escape(terminal)}/payment/{Uri.EscapeDataString(sale.ProviderSessionId)}/cancel", null,
                    cancellationToken);
                return Translate(payment, operationId, fallback: TerminalPaymentState.Canceled);
            }
            catch (PayrexxApiException ex)
            {
                // Nicht als abgebrochen melden. Scheiterte der Abbruch, laeuft der Vorgang weiter - und
                // eine Kasse, die einen Abbruch glaubt, der nicht stattfand, vergisst eine Zahlung.
                LogEnvironment.LogEvent(
                    $"Payrexx did not cancel payment {sale.ProviderSessionId} for sale {sale.TenantSaleId}; it is treated as still running: {ex.OutlineException()}",
                    LogSeverity.Report, PayrexxApiClient.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.InProgress,
                    FailureMessage = ex.Message
                };
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Payrexx kennt keine Zustandsabfrage je Gerät — es gibt nur die Kopplung. Mehr als „ist dieses
        /// Gerät überhaupt mit dem Konto verbunden" lässt sich hier nicht sagen, und das ist ehrlicher,
        /// als aus dem Fehlen einer Antwort „online" zu machen.
        /// </remarks>
        protected override async Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken)
        {
            try
            {
                var paired = await ecr.SendAsync(HttpMethod.Get, $"ecr/{Escape(terminal)}/pair", null, cancellationToken);
                return new TerminalStatus
                {
                    TerminalId = terminal.ProviderTerminalId,
                    Online = paired != null,
                    RawState = paired?.Status,
                    Label = terminal.DisplayName
                };
            }
            catch (PayrexxApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read the pairing state of Payrexx terminal {terminal.ProviderTerminalId}: {ex.OutlineException()}",
                    LogSeverity.Warning, PayrexxApiClient.LogContext);
                return new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = false };
            }
        }

        /// <summary>Übersetzt, was Payrexx über den Vorgang sagt.</summary>
        private static TerminalPaymentOutcome Translate(PayrexxEcrPayment? payment, string operationId,
            TerminalPaymentState fallback = TerminalPaymentState.Unknown)
        {
            if (payment == null)
            {
                return new TerminalPaymentOutcome { OperationId = operationId, State = fallback };
            }

            return new TerminalPaymentOutcome
            {
                OperationId = operationId,
                ProviderPaymentId = payment.PaymentId,
                AmountMinor = payment.Amount > 0 ? payment.Amount : null,
                TipMinor = payment.TipAmount > 0 ? payment.TipAmount : null,
                State = payment.Status?.ToUpperInvariant() switch
                {
                    "SUCCESS" => TerminalPaymentState.Succeeded,
                    "PAYMENT_REQUESTED" or "IN_PROGRESS" => TerminalPaymentState.InProgress,
                    "DECLINED" or "FAILED" => TerminalPaymentState.Failed,
                    "TERMINATED" or "EXPIRED" or "REVERTED" => TerminalPaymentState.Canceled,
                    // UNDERPAID: es ist Geld geflossen, aber nicht genug. Weder bezahlt noch gescheitert -
                    // das muss ein Mensch entscheiden, und bis dahin ist 'unklar' die richtige Antwort.
                    "UNDERPAID" => TerminalPaymentState.Unknown,
                    // Payrexx sagt das selbst so. Genau dafuer gibt es diesen Zustand.
                    "UNKNOWN" => TerminalPaymentState.Unknown,
                    _ => TerminalPaymentState.Unknown
                },
                FailureCode = payment.Status,
                Receipt = BuildReceipt(payment)
            };
        }

        private static TerminalReceipt? BuildReceipt(PayrexxEcrPayment payment)
        {
            if (payment.Slip is not { Count: > 0 } && string.IsNullOrEmpty(payment.CardBrand))
            {
                return null;
            }

            return new TerminalReceipt
            {
                Brand = payment.CardBrand,
                TerminalId = payment.Terminal,
                // Den fertigen Text bevorzugen: was auf einen Kartenbeleg gehoert, weiss der Abwickler
                // besser als wir, und es unterscheidet sich je nach Karte und Land.
                PreformattedText = payment.Slip is { Count: > 0 } ? string.Join('\n', payment.Slip) : null
            };
        }

        private static string Escape(TenantPaymentTerminal terminal)
            => Uri.EscapeDataString(terminal.ProviderTerminalId);
    }
}
