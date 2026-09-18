using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
using Stripe;
using Terminal = Stripe.Terminal;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Impl
{
    /// <summary>
    /// Kassieren an einem Stripe-Terminal, server-gesteuert.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kein SDK auf dem Kassen-PC: der Server legt den PaymentIntent an und schickt ihn an das Gerät
    /// (<c>process_payment_intent</c>). Stripe empfiehlt diesen Weg für BBPOS WisePOS E, Reader
    /// S700/S710 und Verifone.
    /// </para>
    /// <para>
    /// <b>Nicht jedes Stripe-Gerät kann das.</b> Der WisePad 3 etwa verlangt ein Terminal-SDK, und die
    /// gibt es für iOS, Android, JavaScript und React Native — nicht für .NET. Solche Geräte sind über
    /// diesen Weg nicht zu bedienen, und auch der Agent hilft dort nicht: er ist ebenfalls .NET.
    /// </para>
    /// <para>
    /// <b>Derselbe PaymentIntent, nicht ein neuer.</b> Stripe sagt es ausdrücklich: nach einer
    /// abgelehnten Karte wird derselbe wiederverwendet, damit der Kunde es mit einer anderen versuchen
    /// kann. Ein neuer wäre der Weg zur doppelten Belastung. Die Basis sorgt dafür, dass ein zweiter
    /// Anlauf auf derselben Verkaufszeile landet; hier wird die Kennung von dieser Zeile gelesen.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Gerät gelaufen.</b>
    /// </para>
    /// </remarks>
    public class StripeTerminalPaymentService<TContext> : TerminalPaymentServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Der Name, unter dem dieser Weg an einem Gerät eingetragen wird.</summary>
        public const string Key = "stripe";

        private readonly StripeClient client;

        /// <summary>Initializes a new instance of the <see cref="StripeTerminalPaymentService{TContext}"/> class.</summary>
        public StripeTerminalPaymentService(IDbContextFactory<TContext> dbFactory, StripeClient client,
            IGlobalSettings<TenantPaymentsOptions> settings, IEnumerable<IPaymentFeatureGate> featureGates,
            TenantSaleWebhookSink<TContext> sink)
            : base(dbFactory, new StripePaymentsRuntime(settings, featureGates.FirstOrDefault()), sink)
        {
            this.client = client;
        }

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            var account = RequireAccount(sale);
            try
            {
                // Erst die Zahlungsabsicht auf dem Konto des Mandanten - direkt, mit der Provision der
                // Plattform. Dasselbe Modell wie bei den Online-Verkaeufen, nur ohne Zahlungsseite.
                var intent = await new PaymentIntentService(client).CreateAsync(new PaymentIntentCreateOptions
                {
                    Amount = sale.AmountMinor,
                    Currency = sale.Currency.ToLowerInvariant(),
                    // Ohne diesen Eintrag erwartet Stripe eine Online-Zahlungsart, und das Geraet wird die
                    // Absicht nicht annehmen.
                    PaymentMethodTypes = ["card_present"],
                    // Einstufig: autorisieren und einziehen. Zweistufig waere ein eigener Ablauf mit einer
                    // Frist von zwei Tagen und einem Einzugsschritt, den an einer Ladenkasse niemand macht.
                    CaptureMethod = "automatic",
                    ApplicationFeeAmount = sale.ApplicationFeeMinor > 0 ? sale.ApplicationFeeMinor : null,
                    Description = sale.Description,
                    Metadata = new Dictionary<string, string>
                    {
                        ["tenantId"] = sale.TenantId.ToString(),
                        ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                        ["externalReference"] = sale.ExternalReference,
                        ["terminal"] = terminal.ProviderTerminalId
                    }
                }, StripePaymentsRuntime.ForAccount(account,
                    // Der Idempotenz-Schluessel ist die Verkaufszeile: ein doppelt abgeschickter Auftrag
                    // ergibt dieselbe Absicht statt einer zweiten.
                    $"terminal-intent:{sale.TenantSaleId}"), cancellationToken);

                var reader = await new Terminal.ReaderService(client).ProcessPaymentIntentAsync(
                    terminal.ProviderTerminalId,
                    new Terminal.ReaderProcessPaymentIntentOptions
                    {
                        PaymentIntent = intent.Id,
                        ProcessConfig = new Terminal.ReaderProcessConfigOptions
                        {
                            EnableCustomerCancellation = command.AllowCustomerCancellation
                        }
                    },
                    StripePaymentsRuntime.ForAccount(account), cancellationToken);

                return Translate(reader, intent.Id, command.OperationId);
            }
            catch (StripeException ex)
            {
                return TranslateFailure(ex, command.OperationId, sale, terminal);
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var account = RequireAccount(sale);
            if (string.IsNullOrEmpty(sale.ProviderSessionId))
            {
                // Es gibt keine Absicht, also kann auch nichts belastet worden sein. Anders als bei einem
                // ausgebliebenen Ergebnis ist das hier eine Aussage und keine Vermutung.
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            try
            {
                // Die Absicht, nicht das Geraet: sie ist die Quelle der Wahrheit. Das Geraet kann die
                // Verbindung verloren haben, waehrend die Zahlung bei Stripe laengst durch ist - genau der
                // Fall, den die Doku unter "fehlende Webhooks" beschreibt.
                var intent = await new PaymentIntentService(client).GetAsync(sale.ProviderSessionId,
                    new PaymentIntentGetOptions { Expand = ["latest_charge"] },
                    StripePaymentsRuntime.ForAccount(account), cancellationToken);

                return Translate(intent, operationId);
            }
            catch (StripeException ex)
            {
                return TranslateFailure(ex, operationId, sale, terminal);
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var account = RequireAccount(sale);
            try
            {
                var reader = await new Terminal.ReaderService(client).CancelActionAsync(terminal.ProviderTerminalId,
                    null, StripePaymentsRuntime.ForAccount(account), cancellationToken);
                return Translate(reader, sale.ProviderSessionId, operationId);
            }
            catch (StripeException ex) when (ex.StripeError?.Code == "terminal_reader_busy")
            {
                // Die Karte liegt schon auf. Zu spaet - und das ist die richtige Antwort, nicht ein Fehler.
                // Wer hier "abgebrochen" meldete, liesse die Kasse eine Zahlung vergessen, die laeuft.
                LogEnvironment.LogEvent(
                    $"Sale {sale.TenantSaleId} could not be cancelled: reader {terminal.ProviderTerminalId} is already authorising. The payment stands and has to run its course.",
                    LogSeverity.Report, LogContext);
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.InProgress };
            }
            catch (StripeException ex)
            {
                return TranslateFailure(ex, operationId, sale, terminal);
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken)
        {
            // Ohne Konto-Kopfzeile: fuer die blosse Frage, ob ein Geraet online ist, braucht es den
            // Mandanten nicht - und an dieser Stelle steht kein Verkauf zur Verfuegung, aus dem er kaeme.
            try
            {
                var reader = await new Terminal.ReaderService(client).GetAsync(terminal.ProviderTerminalId,
                    cancellationToken: cancellationToken);
                return new TerminalStatus
                {
                    TerminalId = terminal.ProviderTerminalId,
                    // "offline" heisst bei Stripe: seit zwei Minuten kein Lebenszeichen.
                    Online = string.Equals(reader.Status, "online", StringComparison.OrdinalIgnoreCase),
                    Busy = string.Equals(reader.Action?.Status, "in_progress", StringComparison.OrdinalIgnoreCase),
                    RawState = reader.Status,
                    Label = reader.Label
                };
            }
            catch (StripeException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read the state of reader {terminal.ProviderTerminalId}: {ex.OutlineException()}",
                    LogSeverity.Warning, LogContext);
                return new TerminalStatus
                {
                    TerminalId = terminal.ProviderTerminalId,
                    Online = false,
                    RawState = ex.StripeError?.Code
                };
            }
        }

        /// <summary>Übersetzt, was das Gerät gerade tut.</summary>
        private static TerminalPaymentOutcome Translate(Terminal.Reader reader, string? paymentIntentId, string operationId)
            => new()
            {
                OperationId = operationId,
                ProviderPaymentId = paymentIntentId ?? reader.Action?.ProcessPaymentIntent?.PaymentIntentId,
                State = reader.Action?.Status switch
                {
                    "succeeded" => TerminalPaymentState.Succeeded,
                    "failed" => string.Equals(reader.Action.FailureCode, "customer_canceled", StringComparison.Ordinal)
                        ? TerminalPaymentState.Canceled
                        : TerminalPaymentState.Failed,
                    "in_progress" => TerminalPaymentState.InProgress,
                    // Keine Aktion mehr am Geraet heisst nicht, dass nichts geschehen ist - nach einem
                    // Abbruch steht hier nichts. Der Zustand der Absicht entscheidet, nicht dieser hier.
                    _ => TerminalPaymentState.Unknown
                },
                FailureCode = reader.Action?.FailureCode,
                FailureMessage = reader.Action?.FailureMessage
            };

        /// <summary>Übersetzt den Zustand der Zahlungsabsicht — die verlässlichere Quelle.</summary>
        private static TerminalPaymentOutcome Translate(PaymentIntent intent, string operationId)
        {
            var charge = intent.LatestCharge;
            return new TerminalPaymentOutcome
            {
                OperationId = operationId,
                ProviderPaymentId = intent.Id,
                AmountMinor = intent.AmountReceived > 0 ? intent.AmountReceived : intent.Amount,
                State = intent.Status switch
                {
                    "succeeded" => TerminalPaymentState.Succeeded,
                    "requires_capture" => TerminalPaymentState.Authorized,
                    "canceled" => TerminalPaymentState.Canceled,
                    // Wartet weiterhin auf eine Karte: der Vorgang laeuft noch, oder die letzte Karte wurde
                    // abgelehnt und es darf eine zweite versucht werden.
                    "requires_payment_method" => intent.LastPaymentError != null
                        ? TerminalPaymentState.Failed
                        : TerminalPaymentState.InProgress,
                    "requires_confirmation" or "requires_action" or "processing" => TerminalPaymentState.InProgress,
                    _ => TerminalPaymentState.Unknown
                },
                FailureCode = intent.LastPaymentError?.Code,
                FailureMessage = intent.LastPaymentError?.Message,
                Receipt = BuildReceipt(charge)
            };
        }

        /// <summary>
        /// Baut die Angaben für den Kartenbeleg aus der Belastung.
        /// </summary>
        /// <remarks>
        /// Diese Felder sind nicht Zierde: die Anwendungskennung (EMV AID), die Genehmigungsnummer und die
        /// Art der Bestätigung gehören auf einen Kartenbeleg. Stripe liefert sie unter
        /// <c>card_present.receipt</c>, und nur dort — aus dem PaymentIntent allein sind sie nicht zu holen.
        /// </remarks>
        private static TerminalReceipt? BuildReceipt(Charge? charge)
        {
            var present = charge?.PaymentMethodDetails?.CardPresent;
            if (present == null)
            {
                return null;
            }

            return new TerminalReceipt
            {
                Brand = present.Brand,
                MaskedPan = string.IsNullOrEmpty(present.Last4) ? null : $"**** {present.Last4}",
                AuthorizationCode = present.Receipt?.AuthorizationCode,
                ApplicationIdentifier = present.Receipt?.DedicatedFileName,
                ApplicationLabel = present.Receipt?.ApplicationPreferredName,
                VerificationMethod = present.Receipt?.CardholderVerificationMethod,
                TimestampUtc = charge!.Created
            };
        }

        /// <summary>
        /// Übersetzt einen Fehler des Anbieters — und unterscheidet dabei das Entscheidende.
        /// </summary>
        /// <remarks>
        /// <b><c>terminal_reader_timeout</c> ist kein Fehlschlag.</b> Stripe beschreibt ihn ausdrücklich
        /// als möglicherweise falsch negativ: das Gerät hat den Befehl bekommen, nur die Bestätigung kam
        /// nicht zurück. Ihn als Fehlschlag zu buchen, hiesse die Kasse ein zweites Mal kassieren zu
        /// lassen — auf eine Karte, die schon belastet ist.
        /// </remarks>
        private static TerminalPaymentOutcome TranslateFailure(StripeException ex, string operationId,
            TenantSale sale, TenantPaymentTerminal terminal)
        {
            var code = ex.StripeError?.Code;
            var unclear = code is "terminal_reader_timeout";

            LogEnvironment.LogEvent(
                $"Stripe refused a terminal operation for sale {sale.TenantSaleId} on reader {terminal.ProviderTerminalId} (code '{code}'). "
                + (unclear
                    ? "This code can be a FALSE NEGATIVE — the reader may have received the command. The sale stays open and must be asked about, never restarted."
                    : "The sale is left for the caller to decide about.")
                + $" {ex.OutlineException()}",
                unclear ? LogSeverity.Error : LogSeverity.Warning, LogContext);

            return new TerminalPaymentOutcome
            {
                OperationId = operationId,
                State = unclear ? TerminalPaymentState.Unknown : TerminalPaymentState.Failed,
                FailureCode = code,
                FailureMessage = ex.StripeError?.Message ?? ex.Message
            };
        }

        private static string RequireAccount(TenantSale sale)
            => string.IsNullOrEmpty(sale.ProviderAccountId)
                ? throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                    $"Sale {sale.TenantSaleId} has no connected account on record; a terminal payment cannot be addressed.")
                : sale.ProviderAccountId;
    }
}
