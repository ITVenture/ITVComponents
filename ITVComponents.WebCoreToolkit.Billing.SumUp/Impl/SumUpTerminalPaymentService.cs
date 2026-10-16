using System.Globalization;
using ITVComponents.AgentServices.Abstractions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.SumUp.Models;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Impl
{
    /// <summary>
    /// Kassieren an einem SumUp Solo über die Cloud API.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nur Solo, und nur über den eigenen Zugang des Mandanten.</b> Die übrigen SumUp-Geräte (Air, 3G, PIN+)
    /// lassen sich nur aus einer App über Bluetooth steuern, nicht vom Server aus. Und SumUp führt kein Konto für
    /// die Plattform: jeder Mandant bringt seines mit (<c>RequiresOwnAccess</c>).
    /// </para>
    /// <para>
    /// <b>Drei Kennungen, drei Zwecke.</b> Unsere Vorgangskennung geht als <c>affiliate.foreign_transaction_id</c>
    /// mit. Nach ihr lässt sich die Transaktion IMMER finden, auch wenn die Antwort auf den Start ausblieb. Die
    /// <c>client_transaction_id</c> von SumUp steht in <see cref="TenantSale.ProviderSessionId"/>, weil die Meldung
    /// an <c>return_url</c> nur sie nennt. Die <c>checkout_id</c> steht in <see cref="TenantSale.ProviderPaymentIntentId"/>:
    /// nur mit ihr lässt sich der Checkout selbst abfragen, und nur er kennt <c>cancelled</c>. Die Kennung der
    /// bezahlten Transaktion (<c>id</c>) wird zu <see cref="TenantSale.ProviderChargeId"/>, gegen sie wird erstattet.
    /// </para>
    /// <para>
    /// <b>Abbrechen wird nicht bestätigt.</b> <c>terminate</c> nimmt den Auftrag an (202) und meldet nie, ob er
    /// gegriffen hat. Der Vorgang gilt darum weiter als laufend, bis das Nachfragen etwas anderes sagt.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen die SumUp-Sandbox gelaufen.</b> Gebaut nach Doku und OpenAPI-Beschreibung.
    /// </para>
    /// </remarks>
    public class SumUpTerminalPaymentService<TContext> : TerminalPaymentServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Der Name, unter dem dieser Weg an einem Gerät eingetragen wird.</summary>
        public const string Key = SumUpAccess.Key;

        /// <summary>Der Name der Einstellung "Trinkgeld-Vorschläge".</summary>
        public const string TipRatesSetting = "tipRates";

        private readonly SumUpApiClient api;
        private readonly IProviderCredentialStore credentials;

        /// <summary>Initializes a new instance of the <see cref="SumUpTerminalPaymentService{TContext}"/> class.</summary>
        public SumUpTerminalPaymentService(IDbContextFactory<TContext> dbFactory, SumUpApiClient api,
            IProviderCredentialStore credentials, IGlobalSettings<TenantPaymentsOptions> settings,
            IEnumerable<IPaymentFeatureGate> featureGates, TenantSaleWebhookSink<TContext> sink)
            : base(dbFactory, new PaymentsRuntime(settings, featureGates.FirstOrDefault()), sink)
        {
            this.api = api;
            this.credentials = credentials;
        }

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        protected override bool SupportsOwnAccess => true;

        /// <inheritdoc />
        protected override bool RequiresOwnAccess => true;

        /// <inheritdoc />
        public override IReadOnlyList<TerminalSettingDescriptor> DescribeSettings() =>
        [
            new()
            {
                Name = TipRatesSetting,
                Label = "Trinkgeld-Vorschläge",
                Kind = SettingKind.Text,
                HelpText = "Prozentsätze, die das Gerät zur Auswahl anbietet, durch Komma getrennt (z.B. 5, 10, 15). Leer: kein Trinkgeld."
            },
            new()
            {
                Name = TerminalReceipts.TipOnReceiptSetting,
                Label = "Trinkgeld auf den Beleg",
                Kind = SettingKind.Boolean,
                HelpText = "Ein am Terminal gewähltes Trinkgeld erscheint auf dem Kartenbeleg. Gespeichert wird es in jedem Fall."
            }
        ];

        /// <summary>
        /// Koppelt einen Solo mit dem SumUp-Konto des Mandanten.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Am Gerät: abmelden, WLAN einrichten, dann <i>Connections → API → Connect</i>. Der Solo zeigt einen
        /// Code, der fünf Minuten gilt. Mit diesem Code koppelt erst DIESER Aufruf das Gerät. Erst danach gibt es
        /// die Kennung (<c>rdr_…</c>), unter der es angesprochen wird.
        /// </para>
        /// <para>
        /// Gespeichert wird hier nichts: das Ergebnis ist der Entwurf der Geräte-Zeile, den der Aufrufer über
        /// <c>ITerminalAdministration.SaveAsync</c> anlegt.
        /// </para>
        /// </remarks>
        public async Task<TerminalDefinition> PairReaderAsync(int tenantId, string pairingCode, string displayName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pairingCode) || string.IsNullOrWhiteSpace(displayName))
            {
                throw new TenantPaymentException(PaymentErrorCodes.InvalidAmount,
                    "Pairing a SumUp reader needs the code shown on the device and a name for it.");
            }

            var connection = await ConnectForTenantAsync(tenantId, cancellationToken);
            try
            {
                var reader = await api.SendAsync<SumUpReader>(HttpMethod.Post, SumUpAccess.Readers(connection.MerchantCode),
                    new SumUpCreateReaderRequest { PairingCode = pairingCode.Trim(), Name = displayName.Trim() },
                    connection.Credential, cancellationToken);
                if (reader == null || string.IsNullOrWhiteSpace(reader.Id))
                {
                    throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                        "SumUp accepted the pairing but returned no reader id.");
                }

                LogEnvironment.LogEvent(
                    $"SumUp reader {reader.Id} ({reader.Device?.Model ?? "unknown model"}) was paired with merchant {connection.MerchantCode} for tenant {tenantId}; state '{reader.Status}'.",
                    LogSeverity.Report, SumUpApiClient.LogContext);
                return new TerminalDefinition
                {
                    TenantId = tenantId,
                    Provider = Key,
                    ProviderTerminalId = reader.Id,
                    DisplayName = displayName.Trim(),
                    Enabled = true
                };
            }
            catch (SumUpApiException ex) when (ex.StatusCode is 404 or 409)
            {
                LogEnvironment.LogEvent(
                    $"SumUp refused to pair a reader for tenant {tenantId} ({ex.StatusCode}): {ex.Message}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    ex.StatusCode == 404
                        ? "No SumUp reader is waiting for this code. The code is valid for five minutes; start the pairing on the device again."
                        : "This SumUp reader is not waiting for a pairing.", ex);
            }
            catch (SumUpApiException ex)
            {
                throw Translate(ex, $"Pairing a SumUp reader for tenant {tenantId}");
            }
        }

        /// <summary>
        /// Löst die Kopplung eines Solo beim Konto des Mandanten.
        /// </summary>
        /// <remarks>
        /// Das ist nur die Hälfte: am Gerät selbst muss danach noch <i>Connections → API → Disconnect</i> gewählt
        /// werden. Die Geräte-Zeile bleibt bestehen; abgeschaltet wird sie über die Verwaltung.
        /// </remarks>
        public async Task UnpairReaderAsync(int tenantId, string readerId, CancellationToken cancellationToken = default)
        {
            var connection = await ConnectForTenantAsync(tenantId, cancellationToken);
            try
            {
                await api.SendAsync<object>(HttpMethod.Delete, SumUpAccess.Reader(connection.MerchantCode, readerId), null,
                    connection.Credential, cancellationToken);
                LogEnvironment.LogEvent(
                    $"SumUp reader {readerId} was unpaired from merchant {connection.MerchantCode} (tenant {tenantId}). The device itself still has to be disconnected on its screen.",
                    LogSeverity.Report, SumUpApiClient.LogContext);
            }
            catch (SumUpApiException ex)
            {
                throw Translate(ex, $"Unpairing SumUp reader {readerId} for tenant {tenantId}");
            }
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            var options = api.Options;
            if (string.IsNullOrWhiteSpace(options.AffiliateKey) || string.IsNullOrWhiteSpace(options.AffiliateAppId))
            {
                // SumUp lehnt einen Geräte-Checkout ohne Affiliate Key ab. Hier abzulehnen ist dasselbe, nur mit
                // einem Satz, der sagt, wo es fehlt - und sicher, weil nichts am Gerät angekommen ist.
                LogEnvironment.LogEvent(
                    "SumUp terminal payments need 'SumUpPayments:AffiliateKey' and 'SumUpPayments:AffiliateAppId'; the sale is refused before reaching the device.",
                    LogSeverity.Error, SumUpApiClient.LogContext);
                throw new TenantPaymentException(PaymentErrorCodes.ProviderError,
                    "SumUp is not configured: the affiliate key of the platform is missing.");
            }

            if (sale.ApplicationFeeMinor > 0)
            {
                LogEnvironment.LogEvent(
                    $"Terminal sale {sale.TenantSaleId} (tenant {sale.TenantId}) carries a commission of {sale.ApplicationFeeMinor} {sale.Currency}. SumUp withholds nothing for the platform - this amount is a local booking figure only.",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
            }

            var connection = await SumUpAccess.ConnectAsync(await AccessForSaleAsync(sale, cancellationToken), credentials, cancellationToken);
            var request = new SumUpReaderCheckoutRequest
            {
                TotalAmount = new SumUpMoney
                {
                    Currency = sale.Currency.ToUpperInvariant(),
                    MinorUnit = SumUpAccess.MinorUnitOf(sale.Currency),
                    // Kleinste Einheit - nur hier. Die Transaktions-API antwortet in der Haupteinheit.
                    Value = sale.AmountMinor
                },
                Description = string.IsNullOrWhiteSpace(sale.Description) ? null : sale.Description,
                ReturnUrl = string.IsNullOrWhiteSpace(options.WebhookUrl) ? null : options.WebhookUrl,
                TipRates = TipRatesOf(terminal),
                Affiliate = new SumUpAffiliate
                {
                    AppId = options.AffiliateAppId,
                    Key = options.AffiliateKey,
                    ForeignTransactionId = command.OperationId,
                    Tags = string.IsNullOrWhiteSpace(command.Reference) ? null : new Dictionary<string, string> { ["reference"] = command.Reference }
                }
            };

            try
            {
                var created = await api.SendAsync<SumUpData<SumUpReaderCheckoutCreated>>(HttpMethod.Post,
                    $"{SumUpAccess.Reader(connection.MerchantCode, terminal.ProviderTerminalId)}/checkout", request,
                    connection.Credential, cancellationToken);
                var data = created?.Data;
                if (data == null || string.IsNullOrWhiteSpace(data.ClientTransactionId))
                {
                    // Angenommen, aber ohne Kennung: der Auftrag kann am Gerät sein. Nachfragen geht trotzdem -
                    // über unsere eigene Kennung.
                    LogEnvironment.LogEvent(
                        $"SumUp accepted the checkout for sale {sale.TenantSaleId} on reader {terminal.ProviderTerminalId} without returning its ids; it is looked up by our operation id.",
                        LogSeverity.Warning, SumUpApiClient.LogContext);
                    return new TerminalPaymentOutcome { OperationId = command.OperationId, State = TerminalPaymentState.Unknown };
                }

                // Nur der Checkout kennt "abgebrochen"; die Zeile ist verfolgt und wird mit dem Ergebnis gespeichert.
                sale.ProviderPaymentIntentId = data.CheckoutId;
                return new TerminalPaymentOutcome
                {
                    OperationId = command.OperationId,
                    State = TerminalPaymentState.InProgress,
                    ProviderPaymentId = data.ClientTransactionId
                };
            }
            catch (SumUpApiException ex) when (ex.ErrorType == "READER_OFFLINE")
            {
                // Sicher nicht gestartet: das Gerät war nicht erreichbar.
                return new TerminalPaymentOutcome
                {
                    OperationId = command.OperationId,
                    State = TerminalPaymentState.Failed,
                    FailureCode = ex.ErrorType,
                    FailureMessage = "The SumUp reader is offline."
                };
            }
            catch (SumUpApiException ex) when (ex.ErrorType == "READER_BUSY")
            {
                // Besetzt - womöglich mit UNSEREM früheren Versuch, dessen Antwort ausblieb. Nicht als gescheitert
                // buchen: nachfragen, über unsere Kennung.
                LogEnvironment.LogEvent(
                    $"SumUp reader {terminal.ProviderTerminalId} is busy with a pending checkout; sale {sale.TenantSaleId} may be that checkout. It stays open and is looked up by our operation id.",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = command.OperationId,
                    State = TerminalPaymentState.Unknown,
                    FailureCode = ex.ErrorType
                };
            }
            catch (SumUpApiException ex) when (ex.Answered && ex.StatusCode < 500)
            {
                // Abgelehnt mit Antwort: nichts ist am Gerät. Laut werden statt raten.
                throw Translate(ex, $"Starting sale {sale.TenantSaleId} on SumUp reader {terminal.ProviderTerminalId}");
            }

            // Keine Antwort oder ein Fehler bei SumUp selbst: die Ausnahme geht an die Basis, die den Verkauf
            // offen lässt (Unknown) - es ist nicht bekannt, ob die Karte belastet wird.
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var connection = await SumUpAccess.ConnectAsync(await AccessForSaleAsync(sale, cancellationToken), credentials, cancellationToken);

            if (!string.IsNullOrWhiteSpace(sale.ProviderPaymentIntentId))
            {
                try
                {
                    var checkout = (await api.SendAsync<SumUpData<SumUpReaderCheckout>>(HttpMethod.Get,
                        $"{SumUpAccess.Reader(connection.MerchantCode, terminal.ProviderTerminalId)}/checkout/{Uri.EscapeDataString(sale.ProviderPaymentIntentId)}",
                        null, connection.Credential, cancellationToken))?.Data;
                    switch (checkout?.Status?.ToLowerInvariant())
                    {
                        case "pending":
                            return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.InProgress };
                        case "failed":
                            return new TerminalPaymentOutcome
                            {
                                OperationId = operationId, State = TerminalPaymentState.Failed,
                                FailureCode = "failed", FailureMessage = checkout.PaymentFailureReason
                            };
                        case "cancelled":
                            return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Canceled, FailureCode = "cancelled" };
                        case "successful":
                            // Bezahlt - die Einzelheiten (Kennung, Betrag, Karte) stehen an der Transaktion.
                            break;
                        default:
                            LogEnvironment.LogEvent(
                                $"SumUp reported checkout {sale.ProviderPaymentIntentId} of sale {sale.TenantSaleId} as '{checkout?.Status}'; the transaction is asked instead.",
                                LogSeverity.Warning, SumUpApiClient.LogContext);
                            break;
                    }
                }
                catch (SumUpApiException ex) when (ex.StatusCode == 404)
                {
                    // Den Checkout gibt es dort nicht mehr. Die Transaktion entscheidet.
                    LogEnvironment.LogDebugEvent(
                        $"SumUp no longer knows checkout {sale.ProviderPaymentIntentId} of sale {sale.TenantSaleId}; asking the transaction: {ex.Message}",
                        LogSeverity.Report);
                }
            }

            var transaction = await FindTransactionAsync(connection, operationId, cancellationToken);
            if (transaction == null)
            {
                // Noch keine Transaktion: der Kunde hat die Karte noch nicht vorgehalten, oder der Start kam nie an.
                // Welches von beiden, weiss nur das Gerät - und das sagt es uns nicht. Also offen.
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            return await DescribeTransactionAsync(connection, transaction, sale, operationId, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var connection = await SumUpAccess.ConnectAsync(await AccessForSaleAsync(sale, cancellationToken), credentials, cancellationToken);
            try
            {
                await api.SendAsync<object>(HttpMethod.Post,
                    $"{SumUpAccess.Reader(connection.MerchantCode, terminal.ProviderTerminalId)}/terminate", new { },
                    connection.Credential, cancellationToken);
                // 202: angenommen, nicht bestätigt. Ob es gegriffen hat, sagt erst das Nachfragen.
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.InProgress,
                    FailureMessage = "Termination was requested at the reader; SumUp does not confirm it."
                };
            }
            catch (SumUpApiException ex)
            {
                // Nicht als abgebrochen melden. Scheiterte der Abbruch, läuft der Vorgang weiter - und eine Kasse,
                // die einen Abbruch glaubt, der nicht stattfand, vergisst eine Zahlung.
                LogEnvironment.LogEvent(
                    $"SumUp did not take the termination for sale {sale.TenantSaleId} on reader {terminal.ProviderTerminalId}; it is treated as still running: {ex.OutlineException()}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.InProgress,
                    FailureCode = ex.ErrorType,
                    FailureMessage = ex.Message
                };
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Die Statusabfrage braucht einen Solo ab Firmware 3.3.39.0. Ein älteres Gerät antwortet darauf nicht;
        /// dann bleibt nur, ob es noch gekoppelt ist - und daraus "online" zu machen, wäre geraten.
        /// </remarks>
        protected override async Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken)
        {
            SumUpConnection connection;
            try
            {
                connection = await ConnectForTenantAsync(terminal.TenantId, cancellationToken);
            }
            catch (TenantPaymentException ex)
            {
                LogEnvironment.LogEvent(
                    $"The state of SumUp reader {terminal.ProviderTerminalId} cannot be asked: tenant {terminal.TenantId} has no usable SumUp access ({ex.Message}).",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = false, Label = terminal.DisplayName };
            }

            try
            {
                var status = (await api.SendAsync<SumUpData<SumUpReaderStatus>>(HttpMethod.Get,
                    $"{SumUpAccess.Reader(connection.MerchantCode, terminal.ProviderTerminalId)}/status", null,
                    connection.Credential, cancellationToken))?.Data;
                return new TerminalStatus
                {
                    TerminalId = terminal.ProviderTerminalId,
                    Online = string.Equals(status?.Status, "ONLINE", StringComparison.OrdinalIgnoreCase),
                    Busy = status?.State != null && !string.Equals(status.State, "IDLE", StringComparison.OrdinalIgnoreCase),
                    RawState = status?.State ?? status?.Status,
                    Label = terminal.DisplayName
                };
            }
            catch (SumUpApiException ex) when (ex.StatusCode is 400 or 404)
            {
                return await PairingStateAsync(connection, terminal, ex, cancellationToken);
            }
            catch (SumUpApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"Could not read the state of SumUp reader {terminal.ProviderTerminalId}: {ex.OutlineException()}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = false, Label = terminal.DisplayName };
            }
        }

        private async Task<TerminalStatus> PairingStateAsync(SumUpConnection connection, TenantPaymentTerminal terminal,
            SumUpApiException statusError, CancellationToken cancellationToken)
        {
            try
            {
                var reader = await api.SendAsync<SumUpReader>(HttpMethod.Get,
                    SumUpAccess.Reader(connection.MerchantCode, terminal.ProviderTerminalId), null,
                    connection.Credential, cancellationToken);
                LogEnvironment.LogDebugEvent(
                    $"SumUp reader {terminal.ProviderTerminalId} answered no status ({statusError.StatusCode}, probably firmware below 3.3.39.0); its pairing state is '{reader?.Status}'.",
                    LogSeverity.Report);
                return new TerminalStatus
                {
                    TerminalId = terminal.ProviderTerminalId,
                    Online = false,
                    RawState = reader?.Status,
                    Label = terminal.DisplayName
                };
            }
            catch (SumUpApiException ex)
            {
                LogEnvironment.LogEvent(
                    $"SumUp reader {terminal.ProviderTerminalId} is not known to merchant {connection.MerchantCode}: {ex.OutlineException()}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return new TerminalStatus { TerminalId = terminal.ProviderTerminalId, Online = false, Label = terminal.DisplayName };
            }
        }

        /// <summary>
        /// Sucht die Transaktion über UNSERE Vorgangskennung.
        /// </summary>
        /// <remarks>
        /// Über sie und nicht über die Kennungen von SumUp: unsere geht mit dem Start hinaus und ist bekannt, auch
        /// wenn die Antwort auf den Start nie ankam.
        /// </remarks>
        private async Task<SumUpTransaction?> FindTransactionAsync(SumUpConnection connection, string operationId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await api.SendAsync<SumUpTransaction>(HttpMethod.Get,
                    SumUpAccess.Transaction(connection.MerchantCode, $"foreign_transaction_id={Uri.EscapeDataString(operationId)}"),
                    null, connection.Credential, cancellationToken);
            }
            catch (SumUpApiException ex) when (ex.StatusCode == 404)
            {
                LogEnvironment.LogDebugEvent(
                    $"SumUp has no transaction for operation {operationId} at merchant {connection.MerchantCode} yet.",
                    LogSeverity.Report);
                return null;
            }
        }

        private async Task<TerminalPaymentOutcome> DescribeTransactionAsync(SumUpConnection connection,
            SumUpTransaction transaction, TenantSale sale, string operationId, CancellationToken cancellationToken)
        {
            var state = transaction.Status?.ToUpperInvariant() switch
            {
                // REFUNDED heisst: es wurde bezahlt und danach erstattet. Die Erstattung bucht ihr eigener Weg.
                "SUCCESSFUL" or "REFUNDED" => TerminalPaymentState.Succeeded,
                "FAILED" => TerminalPaymentState.Failed,
                "CANCELLED" => TerminalPaymentState.Canceled,
                "PENDING" => TerminalPaymentState.InProgress,
                _ => TerminalPaymentState.Unknown
            };

            if (state != TerminalPaymentState.Succeeded)
            {
                return new TerminalPaymentOutcome { OperationId = operationId, State = state, FailureCode = transaction.Status };
            }

            if (string.IsNullOrWhiteSpace(transaction.Id))
            {
                // Bezahlt, aber ohne Kennung gaebe es nichts, wogegen sich erstatten liesse. Lieber offen lassen
                // und wieder fragen als einen Verkauf buchen, der sich nicht zurueckzahlen laesst.
                LogEnvironment.LogEvent(
                    $"SumUp reports the transaction of sale {sale.TenantSaleId} as paid but without an id; it stays open until the id is known.",
                    LogSeverity.Error, SumUpApiClient.LogContext);
                return new TerminalPaymentOutcome { OperationId = operationId, State = TerminalPaymentState.Unknown };
            }

            SumUpReceipt? receipt = null;
            try
            {
                receipt = await api.SendAsync<SumUpReceipt>(HttpMethod.Get,
                    $"v1.1/receipts/{Uri.EscapeDataString(transaction.Id)}?mid={Uri.EscapeDataString(connection.MerchantCode)}",
                    null, connection.Credential, cancellationToken);
            }
            catch (SumUpApiException ex)
            {
                // Der Beleg entsteht dann aus der Transaktion allein - ohne Autorisierungscode und Terminal-Id.
                LogEnvironment.LogEvent(
                    $"The receipt of SumUp transaction {transaction.Id} (sale {sale.TenantSaleId}) could not be read; the card receipt is built from the transaction alone: {ex.OutlineException()}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
            }

            var currency = transaction.Currency ?? sale.Currency;
            return new TerminalPaymentOutcome
            {
                OperationId = operationId,
                State = TerminalPaymentState.Succeeded,
                // Wird zur Kennung, gegen die erstattet wird.
                ProviderPaymentId = transaction.Id,
                AmountMinor = transaction.Amount is { } amount ? CurrencyMinorUnits.ToMinor(amount, currency) : null,
                TipMinor = transaction.TipAmount is { } tip && tip > 0 ? CurrencyMinorUnits.ToMinor(tip, currency) : null,
                Receipt = SumUpAccess.BuildReceipt(transaction, receipt, connection.MerchantCode, sale.ExternalReference)
            };
        }

        private async Task<SumUpConnection> ConnectForTenantAsync(int tenantId, CancellationToken cancellationToken)
        {
            var access = await AccessForTenantAsync(tenantId, cancellationToken);
            ProviderAccessRouter<TContext>.EnsureSupported(access, SupportsOwnAccess, ProviderKey,
                $"Using the SumUp access of tenant {tenantId}", RequiresOwnAccess);
            return await SumUpAccess.ConnectAsync(access, credentials, cancellationToken);
        }

        /// <summary>Die Trinkgeld-Vorschläge des Geräts als Anteile (5 → 0.05), aufsteigend und ohne Doppelte.</summary>
        internal static decimal[]? TipRatesOf(TenantPaymentTerminal terminal)
        {
            if (string.IsNullOrWhiteSpace(terminal.ConfigurationJson))
            {
                return null;
            }

            string? raw;
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(terminal.ConfigurationJson);
                raw = document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                      && document.RootElement.TryGetProperty(TipRatesSetting, out var value)
                    ? value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : value.GetRawText()
                    : null;
            }
            catch (System.Text.Json.JsonException ex)
            {
                LogEnvironment.LogEvent(
                    $"The settings of SumUp reader {terminal.ProviderTerminalId} are not readable JSON; no tip is offered: {ex.OutlineException()}",
                    LogSeverity.Warning, SumUpApiClient.LogContext);
                return null;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var rates = new SortedSet<decimal>();
            foreach (var part in raw.Split([',', ';', ' ', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (decimal.TryParse(part.Trim().TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var percent)
                    && percent is >= 1 and <= 99)
                {
                    // SumUp will Anteile zwischen 0.01 und 0.99, in Schritten von 0.01.
                    rates.Add(Math.Round(percent / 100m, 2));
                }
                else
                {
                    LogEnvironment.LogEvent(
                        $"Tip rate '{part}' of SumUp reader {terminal.ProviderTerminalId} is not a percentage between 1 and 99; it is left out.",
                        LogSeverity.Warning, SumUpApiClient.LogContext);
                }
            }

            return rates.Count == 0 ? null : [.. rates];
        }

        private static TenantPaymentException Translate(SumUpApiException ex, string what)
        {
            if (ex.StatusCode is 401 or 403)
            {
                LogEnvironment.LogEvent($"{what}: SumUp rejected the tenant's access ({ex.StatusCode}). The key or login has to be renewed. {ex.Message}",
                    LogSeverity.Error, SumUpApiClient.LogContext);
                return new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    "SumUp rejected the stored access; it has to be renewed.", ex);
            }

            LogEnvironment.LogEvent($"{what} was refused by SumUp: {ex.OutlineException()}", LogSeverity.Error, SumUpApiClient.LogContext);
            return new TenantPaymentException(PaymentErrorCodes.ProviderError, ex.Message, ex);
        }
    }
}
