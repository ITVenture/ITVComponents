using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.AgentServices.Abstractions;
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
using WalleeConfiguration = Wallee.Client.Configuration;

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
        private readonly IProviderCredentialStore? credentials;

        /// <summary>Initializes a new instance of the <see cref="WalleeTerminalPaymentService{TContext}"/> class.</summary>
        public WalleeTerminalPaymentService(IDbContextFactory<TContext> dbFactory,
            IGlobalSettings<WalleeOptions> walleeSettings, IGlobalSettings<TenantPaymentsOptions> settings,
            IEnumerable<IPaymentFeatureGate> featureGates, TenantSaleWebhookSink<TContext> sink,
            IProviderCredentialStore? credentials = null)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()), sink)
        {
            this.walleeSettings = walleeSettings;
            this.credentials = credentials;
        }

        private WalleeOptions Wallee => walleeSettings.Value;

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        /// <remarks>Ein eigener Zugang ist ein Space im eigenen wallee-Konto des Mandanten; das Gerät muss dort angelegt sein.</remarks>
        protected override bool SupportsOwnAccess => true;

        /// <summary>Konto und Space, in denen dieser Verkauf läuft - eingefroren beim Anlegen.</summary>
        private async Task<WalleeConnection> ConnectionForAsync(TenantSale sale, CancellationToken cancellationToken)
            => await WalleeConnection.ForAsync(await AccessForSaleAsync(sale, cancellationToken), Wallee,
                sale.ProviderAccountId, credentials, cancellationToken);

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
                Kind = SettingKind.Text,
                HelpText = "Leer heisst: die Vorgabe aus den wallee-Einstellungen."
            },
            new()
            {
                Name = TerminalReceipts.TipOnReceiptSetting,
                Label = "Trinkgeld auf den Beleg",
                Kind = SettingKind.Boolean,
                HelpText = "Ein am Terminal erfasstes Trinkgeld erscheint auf dem Kartenbeleg. Gespeichert wird es in jedem Fall."
            }
        ];

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            var wallee = Wallee;
            var connection = await ConnectionForAsync(sale, cancellationToken);
            var space = connection.SpaceId;
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
                var configuration = connection.Configuration;
                var transaction = await Task.Run(
                        () => new TransactionsService(configuration).PostPaymentTransactions(space, create),
                        cancellationToken)
                    .ConfigureAwait(false);

                // Die Transaktion steht jetzt bei wallee. Ihre Kennung muss auf die Zeile, BEVOR das Geraet sie
                // bekommt: sonst findet der Webhook den Verkauf nicht, und nach einer ausbleibenden Antwort ist
                // nichts mehr nachzufragen.
                await RememberProviderPaymentIdAsync(sale, transaction.Id.ToString(), cancellationToken)
                    .ConfigureAwait(false);

                // Jetzt erst das Geraet. Reihenfolge der Parameter: Geraet, Transaktion, Raum.
                var started = await Task.Run(
                        () => new PaymentTerminalsService(configuration)
                            .PostPaymentTerminalsIdPerformTransaction(terminalId, transaction.Id, space,
                                wallee.DefaultLanguage),
                        cancellationToken)
                    .ConfigureAwait(false);

                return await WithReceiptAsync(Translate(started, command.OperationId), started, configuration, space,
                    wallee, sale, cancellationToken);
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
            var connection = await ConnectionForAsync(sale, cancellationToken);
            var space = connection.SpaceId;
            try
            {
                var configuration = connection.Configuration;
                var transaction = await Task.Run(
                        () => new TransactionsService(configuration).GetPaymentTransactionsId(transactionId, space),
                        cancellationToken)
                    .ConfigureAwait(false);
                return await WithReceiptAsync(Translate(transaction, operationId), transaction, configuration, space,
                    wallee, sale, cancellationToken);
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

            var connection = await ConnectionForAsync(sale, cancellationToken);
            var space = connection.SpaceId;
            try
            {
                await Task.Run(
                        () => new TransactionsService(connection.Configuration)
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
            var terminalId = RequireTerminalId(terminal);

            // Kein Verkauf, also der Zugang, der jetzt fuer den Mandanten gilt. Auf dem Plattform-Konto bleibt es
            // beim Space aus den Einstellungen - wie bisher.
            WalleeConnection connection;
            try
            {
                connection = await WalleeConnection.ForAsync(await AccessForTenantAsync(terminal.TenantId, cancellationToken),
                    Wallee, null, credentials, cancellationToken);
            }
            catch (TenantPaymentException ex) when (ex.Code == PaymentErrorCodes.OwnAccessUnavailable)
            {
                LogEnvironment.LogEvent(
                    $"The state of wallee terminal {terminalId} cannot be asked: the own access of tenant {terminal.TenantId} is not usable ({ex.Message}).",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
                return new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = false };
            }

            try
            {
                var device = await Task.Run(
                        () => new PaymentTerminalsService(connection.Configuration)
                            .GetPaymentTerminalsId(terminalId, connection.SpaceId),
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

        /// <summary>
        /// Holt zu einer bezahlten Transaktion den Kartenbeleg und das Trinkgeld.
        /// </summary>
        /// <remarks>
        /// <para>
        /// wallee liefert den Beleg <b>fertig gesetzt</b> (<c>GetPaymentTransactionsIdTerminalReceipts</c>); er
        /// wird so übernommen und nicht in Felder zerlegt. Es kann mehrere geben (Kunden- und Händlerbeleg) —
        /// auf den Bon gehört der des Kunden, darum wird der bevorzugt.
        /// </para>
        /// <para>
        /// Scheitert der Abruf, bleibt die Zahlung bezahlt — nur ohne Beleg. Das wird protokolliert, nicht
        /// verschwiegen: ein Beleg ohne Kartendaten fällt sonst erst an der Kasse auf.
        /// </para>
        /// </remarks>
        private async Task<TerminalPaymentOutcome> WithReceiptAsync(TerminalPaymentOutcome outcome,
            Transaction transaction, WalleeConfiguration configuration, long space, WalleeOptions wallee,
            TenantSale sale, CancellationToken cancellationToken)
        {
            if (outcome.State != TerminalPaymentState.Succeeded)
            {
                return outcome;
            }

            var tip = transaction.LineItems?
                .Where(i => i.Type == LineItemType.TIP)
                .Sum(i => i.AmountIncludingTax) ?? 0m;
            if (tip > 0 && transaction.Currency != null)
            {
                outcome.TipMinor = CurrencyMinorUnits.ToMinor(tip, transaction.Currency);
            }

            try
            {
                var receipts = await Task.Run(
                        () => new TransactionsService(configuration).GetPaymentTransactionsIdTerminalReceipts(
                            transaction.Id, TerminalReceiptFormat.TXT, Math.Max(20, wallee.TerminalReceiptWidth), space),
                        cancellationToken)
                    .ConfigureAwait(false);
                var chosen = receipts?.Data?
                                 .OrderByDescending(r => IsCustomerReceipt(r.ReceiptType))
                                 .FirstOrDefault(r => r.Data is { Length: > 0 });
                if (chosen == null)
                {
                    LogEnvironment.LogEvent(
                        $"wallee returned no terminal receipt for transaction {transaction.Id} (sale {sale.TenantSaleId}). The sale is paid, but the receipt lacks the card details.",
                        LogSeverity.Warning, WalleeRuntime.LogContext);
                    return outcome;
                }

                outcome.Receipt = new TerminalReceipt
                {
                    MerchantReference = transaction.MerchantReference,
                    TimestampUtc = transaction.AuthorizedOn,
                    PreformattedText = System.Text.Encoding.UTF8.GetString(chosen.Data)
                };
            }
            catch (ApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not fetch the terminal receipt of wallee transaction {transaction.Id} (sale {sale.TenantSaleId}). The sale is paid, but the receipt lacks the card details: {ex.OutlineException()}",
                    LogSeverity.Warning, WalleeRuntime.LogContext);
            }

            return outcome;
        }

        /// <summary>Ob ein Belegtyp der des Kunden ist — wallee benennt ihn nur in Klartext je Sprache.</summary>
        private static bool IsCustomerReceipt(PaymentTerminalReceiptType? type)
            => type?.Name?.Values.Any(n => n.Contains("customer", StringComparison.OrdinalIgnoreCase)
                                           || n.Contains("kunde", StringComparison.OrdinalIgnoreCase)) == true;

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
    }
}
