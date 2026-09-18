using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Billing.Wallee.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;
using Wallee.Client;
using Wallee.Model;
using Wallee.Service;

namespace ITVComponents.WebCoreToolkit.Billing.Wallee.Impl
{
    /// <summary>
    /// Kassieren an einem wallee-Terminal über die Cloud (Cloud Till Interface).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Zwei Aufrufe: eine Transaktion anlegen wie bei einem Online-Verkauf, dann das Gerät damit
    /// beauftragen (<c>PostPaymentTerminalsIdPerformTransaction</c>). Das Ergebnis steht danach in
    /// derselben Transaktion — <b>eine Terminalzahlung ist bei wallee kein eigener Vorgangstyp</b>,
    /// weshalb sie auch im bestehenden Webhook-Weg auftaucht.
    /// </para>
    /// <para>
    /// <b>Die Alternative wäre LTI</b>, die lokale Schnittstelle: ein Socket vom Kassen-PC direkt zum
    /// Gerät im selben Netz, schneller, aber nur von dort erreichbar. Dafür gibt es den Agenten-Weg;
    /// diese Klasse ist die Cloud-Variante, die von überall funktioniert.
    /// </para>
    /// <para>
    /// <b>Wie bei Achse B gibt es keine Provision</b>, die wallee einbehielte. Sie steht auf der Zeile
    /// und muss getrennt verrechnet werden — der Verkaufsdienst sagt das bei jedem Verkauf, hier gilt
    /// dasselbe.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Gerät gelaufen.</b>
    /// </para>
    /// </remarks>
    public class WalleeTerminalPaymentService<TContext> : TerminalPaymentServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Der Name, unter dem dieser Weg an einem Gerät eingetragen wird.</summary>
        public const string Key = "wallee";

        private readonly IGlobalSettings<WalleeOptions> walleeSettings;

        /// <summary>Initializes a new instance of the <see cref="WalleeTerminalPaymentService{TContext}"/> class.</summary>
        public WalleeTerminalPaymentService(IDbContextFactory<TContext> dbFactory,
            IGlobalSettings<WalleeOptions> walleeSettings, IGlobalSettings<TenantPaymentsOptions> settings,
            IEnumerable<IPaymentFeatureGate> featureGates, TenantSaleWebhookSink<TContext> sink)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()), sink)
        {
            this.walleeSettings = walleeSettings;
        }

        private WalleeOptions Wallee => walleeSettings.Value;

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        /// <remarks>
        /// wallee adressiert Terminals über eine Zahl; die gehört in die Gerätekennung. Der Raum kommt
        /// vom Konto des Mandanten oder aus den Einstellungen.
        /// </remarks>
        public override IReadOnlyList<TerminalSettingDescriptor> DescribeSettings() =>
        [
            new()
            {
                Name = "language",
                Label = "Sprache am Gerät",
                Kind = TerminalSettingKind.Text,
                HelpText = "Leer heisst: die Vorgabe aus den wallee-Einstellungen."
            }
        ];

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            var wallee = Wallee;
            var space = ResolveSpace(sale, wallee);
            var terminalId = RequireTerminalId(terminal);

            if (sale.ApplicationFeeMinor > 0)
            {
                LogEnvironment.LogEvent(
                    $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) carries a commission of {sale.ApplicationFeeMinor} {sale.Currency}, but wallee has no marketplace split — the amount is booked locally and must be invoiced to the tenant separately.",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
            }

            var create = new TransactionCreate
            {
                Currency = sale.Currency.ToUpperInvariant(),
                MerchantReference = sale.ExternalReference,
                // Ohne dies bliebe die Transaktion in PENDING stehen und wartete auf eine Bestaetigung,
                // die in diesem Ablauf niemand schickt.
                AutoConfirmationEnabled = true,
                LineItems =
                [
                    new LineItemCreate
                    {
                        Name = sale.Description,
                        UniqueId = sale.TenantSaleId.ToString(),
                        Quantity = 1,
                        // Dezimal in der HAUPTeinheit - andersherum als bei Stripe und Payrexx.
                        AmountIncludingTax = CurrencyMinorUnits.ToMajor(sale.AmountMinor, sale.Currency),
                        // PFLICHT: die Vorbelegung 0 ist kein gueltiger Wert dieses Aufzaehlungstyps.
                        Type = LineItemType.PRODUCT
                    }
                ],
                MetaData = new Dictionary<string, string>
                {
                    ["tenantId"] = sale.TenantId.ToString(),
                    ["tenantSaleId"] = sale.TenantSaleId.ToString(),
                    ["externalReference"] = sale.ExternalReference,
                    ["terminal"] = terminal.ProviderTerminalId
                }
            };

            try
            {
                var configuration = WalleeRuntime.Configure(wallee);
                var transaction = await Task.Run(
                        () => new TransactionsService(configuration).PostPaymentTransactions(space, create),
                        cancellationToken)
                    .ConfigureAwait(false);

                // Jetzt erst das Geraet. Reihenfolge der Parameter: Geraet, Transaktion, Raum.
                var started = await Task.Run(
                        () => new PaymentTerminalsService(configuration)
                            .PostPaymentTerminalsIdPerformTransaction(terminalId, transaction.Id, space,
                                wallee.DefaultLanguage),
                        cancellationToken)
                    .ConfigureAwait(false);

                return Translate(started, command.OperationId);
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"wallee refused a terminal payment for sale {sale.TenantSaleId} on terminal {terminalId} (space {space}). The sale stays open — it is NOT known whether the card was charged: {ex.OutlineException()}",
                    LogSeverity.Error, WalleeRuntime.LogContext);
                // Bewusst Unknown und nicht Failed: eine Ausnahme sagt, dass wir keine Antwort haben, nicht
                // dass nichts geschehen ist.
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
            if (!long.TryParse(sale.ProviderSessionId, out var transactionId))
            {
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            var wallee = Wallee;
            var space = ResolveSpace(sale, wallee);
            try
            {
                var transaction = await Task.Run(
                        () => new TransactionsService(WalleeRuntime.Configure(wallee))
                            .GetPaymentTransactionsId(transactionId, space),
                        cancellationToken)
                    .ConfigureAwait(false);
                return Translate(transaction, operationId);
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not ask wallee about transaction {transactionId} (space {space}) for sale {sale.TenantSaleId}: {ex.OutlineException()}",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = ex.Message
                };
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// wallee kennt keinen Abbruch am Gerät. Was hier geht, ist die Transaktion zu annullieren — und
        /// das greift nur, solange sie noch nicht autorisiert ist. Scheitert es, ist die richtige Antwort
        /// „läuft weiter", nicht „abgebrochen": eine Kasse, die einen Abbruch glaubt, der nicht
        /// stattgefunden hat, vergisst eine Zahlung.
        /// </remarks>
        protected override async Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            if (!long.TryParse(sale.ProviderSessionId, out var transactionId))
            {
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            var wallee = Wallee;
            var space = ResolveSpace(sale, wallee);
            try
            {
                await Task.Run(
                        () => new TransactionsService(WalleeRuntime.Configure(wallee))
                            .PostPaymentTransactionsIdVoidOnline(transactionId, space),
                        cancellationToken)
                    .ConfigureAwait(false);
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Canceled };
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"wallee did not void transaction {transactionId} for sale {sale.TenantSaleId}; the payment is treated as still running: {ex.OutlineException()}",
                    LogSeverity.Report, WalleeRuntime.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.InProgress,
                    FailureMessage = ex.Message
                };
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken)
        {
            var wallee = Wallee;
            var terminalId = RequireTerminalId(terminal);
            if (wallee.SpaceId <= 0)
            {
                throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                    "No 'WalleePayments:SpaceId' is configured, so a terminal cannot be looked up.");
            }

            try
            {
                var device = await Task.Run(
                        () => new PaymentTerminalsService(WalleeRuntime.Configure(wallee))
                            .GetPaymentTerminalsId(terminalId, wallee.SpaceId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new TerminalStatus
                {
                    TerminalId = terminal.ProviderTerminalId,
                    Online = device.State == PaymentTerminalState.ACTIVE,
                    RawState = device.State?.ToString(),
                    Label = device.Name
                };
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read the state of wallee terminal {terminalId}: {ex.OutlineException()}",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = false };
            }
        }

        /// <summary>Übersetzt den Zustand einer wallee-Transaktion.</summary>
        private static TerminalPaymentOutcome Translate(Transaction transaction, string operationId)
            => new()
            {
                OperationId = operationId,
                ProviderPaymentId = transaction.Id.ToString(),
                AmountMinor = transaction.Currency == null
                    ? null
                    : CurrencyMinorUnits.ToMinor(transaction.CompletedAmount, transaction.Currency),
                State = transaction.State switch
                {
                    // FULFILL heisst bei wallee: liefern. COMPLETED: eingezogen. Beides ist bezahlt.
                    TransactionState.FULFILL or TransactionState.COMPLETED => TerminalPaymentState.Succeeded,
                    // Reserviert, aber nicht eingezogen - wer hier freigaebe, lieferte gegen ein Versprechen.
                    TransactionState.AUTHORIZED => TerminalPaymentState.Authorized,
                    TransactionState.FAILED or TransactionState.DECLINE => TerminalPaymentState.Failed,
                    TransactionState.VOIDED => TerminalPaymentState.Canceled,
                    TransactionState.CREATE or TransactionState.PENDING or TransactionState.CONFIRMED
                        or TransactionState.PROCESSING => TerminalPaymentState.InProgress,
                    _ => TerminalPaymentState.Unknown
                },
                FailureMessage = transaction.UserFailureMessage
            };

        /// <summary>Die Gerätekennung bei wallee ist eine Zahl.</summary>
        private static long RequireTerminalId(TenantPaymentTerminal terminal)
            => long.TryParse(terminal.ProviderTerminalId, out var id) && id > 0
                ? id
                : throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    $"Terminal '{terminal.DisplayName}' has '{terminal.ProviderTerminalId}' on record, but wallee addresses terminals by a numeric id.");

        /// <summary>Der Raum: der des Mandanten, sonst der aus den Einstellungen.</summary>
        private static long ResolveSpace(TenantSale sale, WalleeOptions options)
        {
            if (!string.IsNullOrWhiteSpace(sale.ProviderAccountId)
                && long.TryParse(sale.ProviderAccountId, out var space) && space > 0)
            {
                return space;
            }

            if (options.SpaceId <= 0)
            {
                throw new TenantPaymentException(PaymentErrorCodes.NoAccount,
                    "Neither the sale nor the 'WalleePayments' setting names a wallee space to work in.");
            }

            return options.SpaceId;
        }
    }
}
