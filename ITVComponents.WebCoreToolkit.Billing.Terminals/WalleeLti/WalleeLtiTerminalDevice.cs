using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;

namespace ITVComponents.WebCoreToolkit.Billing.Terminals.WalleeLti
{
    /// <summary>
    /// Ein wallee-Terminal, das über das lokale Netz angesprochen wird (Local Till Interface).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Läuft auf dem <b>Kassen-Agenten</b>, nicht in der Web-Anwendung: LTI spricht das Gerät direkt
    /// im LAN an, von aussen ist es nicht erreichbar. Die Web-Anwendung ruft diese Klasse über ihren
    /// Proxy.
    /// </para>
    /// <para>
    /// <b>Die wichtigste Eigenheit dieses Protokolls, und sie ist gefährlich:</b> die
    /// <c>trxSyncNumber</c> ist keine gewöhnliche Wiederholungs-Kennung. Dieselbe Nummer erneut zu
    /// schicken heisst nicht „sag mir, was aus dem Vorgang wurde", sondern <b>„storniere die letzte
    /// Transaktion und führe diese aus"</b>. Von einer solchen Rücknahme erfährt die Kasse erst über
    /// den Tagesabschluss-Beleg.
    /// </para>
    /// <para>
    /// Deshalb wird hier zum Nachfragen der <c>reprintReceiptRequest</c> benutzt — die Dokumentation
    /// nennt ihn selbst „the simplest approach". Eine Zahlung wird <b>nie</b> von selbst wiederholt;
    /// das Auto-Reversal bleibt einem bewussten neuen Anlauf vorbehalten, den die Web-Seite anstösst.
    /// </para>
    /// <para>
    /// <b>Noch nicht gegen ein echtes Gerät gelaufen.</b> Nachrichtennamen und Felder stammen aus der
    /// Dokumentation von wallee (LTI 2.51).
    /// </para>
    /// </remarks>
    public class WalleeLtiTerminalDevice : ITerminalDevice
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNameCaseInsensitive = true,
            // Eine Maske liefert Text, auch fuer Zahlen. TerminalSettings.Compose schreibt sie zwar
            // richtig, aber ein von Hand gepflegtes JSON tut das vielleicht nicht - und an einem
            // '"port": "50000"' soll die Kasse nicht scheitern.
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        /// <inheritdoc />
        public async Task<TerminalPaymentOutcome> StartPaymentAsync(TerminalTarget target,
            TerminalPaymentCommand command, CancellationToken cancellationToken = default)
        {
            var terminal = Read(target);
            var receipt = new ReceiptCollector();

            var request = new XElement(WalleeLtiConnection.Pos + "financialTrxRequest",
                new XAttribute(XNamespace.Xmlns + "vcs-pos", WalleeLtiConnection.Pos.NamespaceName),
                new XElement("posId", terminal.PosId),
                new XElement("trxSyncNumber", SyncNumber(command.OperationId)),
                new XElement("trxData",
                    // Kleinste Einheit, wie ueberall in diesem Umfeld.
                    new XElement("amount", command.AmountMinor),
                    // ACHTUNG: der NUMERISCHE ISO-Code (756), nicht "CHF". Mit dem Buchstabencode
                    // weist das Geraet die Anfrage zurueck.
                    new XElement("currency", NumericCurrency(command.Currency)),
                    // 0 = Kauf.
                    new XElement("transactionType", 0),
                    new XElement("noDCC", terminal.SuppressDynamicCurrencyConversion),
                    // Die VORGANGSkennung, nicht die Bestellnummer: beim Nachfragen kennt der Aufrufer
                    // nur sie, und sie ist das einzige Merkmal, an dem sich die letzte Transaktion des
                    // Geraets diesem Vorgang zuordnen laesst.
                    new XElement("merchantReference", command.OperationId)),
                new XElement("receiptFormat", terminal.ReceiptFormat),
                new XElement("tillMode", "SDK"),
                new XElement("showTrxResultScreens", terminal.ShowTransactionResultScreens));

            return await ExchangeAsync(terminal, request, "financialTrxResponse", command.OperationId, receipt,
                terminal.TransactionTimeoutSeconds, cancellationToken);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Fragt die <b>letzte</b> Transaktion des Geräts ab — eine Abfrage nach Vorgangskennung gibt es
        /// bei LTI nicht. Gehört sie nicht zu diesem Vorgang, ist die Antwort <c>Unknown</c> und keine
        /// Vermutung: es gibt keinen Weg, „nicht auffindbar" von „nicht geschehen" zu unterscheiden,
        /// und der Unterschied ist genau der zwischen einer offenen und einer doppelten Belastung.
        /// </remarks>
        public async Task<TerminalPaymentOutcome> GetPaymentAsync(TerminalTarget target, string operationId,
            CancellationToken cancellationToken = default)
        {
            var terminal = Read(target);
            var request = new XElement(WalleeLtiConnection.Pos + "reprintReceiptRequest",
                new XAttribute(XNamespace.Xmlns + "vcs-pos", WalleeLtiConnection.Pos.NamespaceName),
                new XElement("type", "TRX"));

            var receipt = new ReceiptCollector();
            var outcome = await ExchangeAsync(terminal, request, "reprintReceiptResponse", operationId, receipt,
                terminal.ConnectTimeoutSeconds + 30, cancellationToken);

            var seen = outcome.Receipt?.MerchantReference;
            if (outcome.State == TerminalPaymentState.Succeeded
                && !string.IsNullOrEmpty(seen)
                && !string.Equals(seen, operationId, StringComparison.Ordinal))
            {
                // Es gibt eine letzte Transaktion, aber sie ist nicht unsere. Das heisst NICHT, dass
                // unsere nicht stattgefunden hat - sie kann aelter sein. Also weiterhin unklar.
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = "The last transaction on this terminal belongs to a different operation."
                };
            }

            return outcome;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <c>reversalRequest</c> nimmt die <b>letzte</b> Transaktion zurück. Ist sie noch im Gange,
        /// scheitert das — und dann ist „läuft weiter" die richtige Antwort.
        /// </remarks>
        public async Task<TerminalPaymentOutcome> CancelPaymentAsync(TerminalTarget target, string operationId,
            CancellationToken cancellationToken = default)
        {
            var terminal = Read(target);
            var request = new XElement(WalleeLtiConnection.Pos + "reversalRequest",
                new XAttribute(XNamespace.Xmlns + "vcs-pos", WalleeLtiConnection.Pos.NamespaceName),
                new XElement("posId", terminal.PosId),
                new XElement("receiptFormat", terminal.ReceiptFormat),
                new XElement("tillMode", "SDK"),
                new XElement("showTrxResultScreens", terminal.ShowTransactionResultScreens));

            try
            {
                var receipt = new ReceiptCollector();
                await ExchangeAsync(terminal, request, "reversalResponse", operationId, receipt,
                    terminal.TransactionTimeoutSeconds, cancellationToken);
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.Canceled,
                    Receipt = receipt.Build()
                };
            }
            catch (TerminalDeviceException ex)
            {
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
        /// LTI kennt keine Erstattung im Sinn einer späteren Gutschrift — <c>reversalRequest</c> nimmt
        /// nur die letzte Transaktion zurück, und nur bis zum Tagesabschluss. Alles andere läuft über
        /// das Portal des Anbieters und nicht über das Gerät.
        /// </remarks>
        public Task<TerminalPaymentOutcome> RefundPaymentAsync(TerminalTarget target,
            TerminalRefundCommand command, CancellationToken cancellationToken = default)
            => throw new TerminalDeviceException(
                "A wallee terminal on the local till interface cannot refund an older payment: reversalRequest only takes back the LAST transaction, and only until the daily balance has run. Refund it through the wallee back office instead.");

        /// <inheritdoc />
        /// <remarks>
        /// Über <c>pingRequest</c>. Mehr als „antwortet das Gerät" lässt sich hier nicht sagen, und das
        /// ist ehrlicher, als aus dem Ausbleiben eines Fehlers „bereit" zu machen.
        /// </remarks>
        public async Task<TerminalStatus> GetStatusAsync(TerminalTarget target, CancellationToken cancellationToken = default)
        {
            var terminal = Read(target);
            try
            {
                await using var connection = await WalleeLtiConnection.ConnectAsync(terminal.Host, terminal.Port,
                    TimeSpan.FromSeconds(terminal.ConnectTimeoutSeconds), cancellationToken);

                var request = new XElement(WalleeLtiConnection.Pos + "pingRequest",
                    new XAttribute(XNamespace.Xmlns + "vcs-pos", WalleeLtiConnection.Pos.NamespaceName),
                    new XElement("posId", terminal.PosId));

                await connection.ExchangeAsync(request, "pingResponse", null, cancellationToken);
                return new TerminalStatus { TerminalId = target.TerminalId, Online = true, RawState = "reachable" };
            }
            catch (TerminalDeviceException ex)
            {
                return new TerminalStatus { TerminalId = target.TerminalId, Online = false, RawState = ex.Message };
            }
        }

        /// <summary>Führt einen Austausch mit dem Gerät und übersetzt die Antwort.</summary>
        private async Task<TerminalPaymentOutcome> ExchangeAsync(WalleeLtiTerminalOptions terminal, XElement request,
            string responseName, string operationId, ReceiptCollector receipt, int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            using var timed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timed.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                await using var connection = await WalleeLtiConnection.ConnectAsync(terminal.Host, terminal.Port,
                    TimeSpan.FromSeconds(terminal.ConnectTimeoutSeconds), cancellationToken);

                var response = await connection.ExchangeAsync(request, responseName, receipt.Collect, timed.Token);
                return Translate(response, operationId, receipt);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Zeit abgelaufen. Das Geraet kann die Zahlung trotzdem durchgefuehrt haben - deshalb
                // UNKNOWN und nicht Failed. Der Aufrufer fragt nach, er startet nicht neu.
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.Unknown,
                    FailureCode = "timeout",
                    FailureMessage = $"The terminal did not answer within {timeoutSeconds} seconds. Whether the card was charged is NOT known.",
                    Receipt = receipt.Build()
                };
            }
            catch (TerminalDeviceException ex)
            {
                return new TerminalPaymentOutcome
                {
                    OperationId = operationId,
                    State = TerminalPaymentState.Unknown,
                    FailureMessage = ex.Message,
                    Receipt = receipt.Build()
                };
            }
        }

        /// <summary>Übersetzt die Antwort des Geräts.</summary>
        private static TerminalPaymentOutcome Translate(XElement response, string operationId, ReceiptCollector receipt)
        {
            // Bei reprintReceiptResponse stecken die Felder eine Ebene tiefer, unter lastTrx.
            var body = response.Element("lastTrx") ?? response;

            var result = ReadInt(body, "trxResult");
            var amount = ReadLong(body, "amountAuth");
            var tip = ReadLong(body, "amountTip");

            var state = result switch
            {
                // 0 ist der genehmigte Fall. Alles andere ist NICHT automatisch ein Fehlschlag: die Liste
                // der Werte gehoert wallee, und ein unbekannter darf nicht als "nichts passiert" gelten.
                0 => TerminalPaymentState.Succeeded,
                null => TerminalPaymentState.Unknown,
                _ => TerminalPaymentState.Failed
            };

            return new TerminalPaymentOutcome
            {
                OperationId = operationId,
                // Die Belegnummer des Geraets ist das, woran sich der Vorgang spaeter wiederfinden laesst.
                ProviderPaymentId = body.Element("transactionRefNumber")?.Value
                                    ?? body.Element("ep2TrxSeqCnt")?.Value,
                State = state,
                AmountMinor = amount,
                TipMinor = tip is > 0 ? tip : null,
                FailureCode = result?.ToString(CultureInfo.InvariantCulture),
                FailureMessage = body.Element("ep2AuthResponseCode")?.Value is { } code && state == TerminalPaymentState.Failed
                    ? $"The terminal answered with authorisation code '{code}'."
                    : null,
                Receipt = receipt.Build(body)
            };
        }

        /// <summary>
        /// Die Synchronisationsnummer für diesen Vorgang.
        /// </summary>
        /// <remarks>
        /// <b>Absichtlich die Vorgangskennung und kein eigener Zähler.</b> Sie ist je Verkauf eindeutig
        /// und überlebt einen Neustart der Kasse — ein lokaler Zähler täte weder das eine noch das
        /// andere, und wenn er nach einem Neustart wieder bei eins begänne, nähme das Gerät eine
        /// fremde Zahlung zurück.
        /// <para>
        /// Dass ein <b>bewusster</b> zweiter Anlauf derselben Bestellung dieselbe Nummer trägt und damit
        /// die vorige Transaktion zurücknimmt, ist genau richtig: dann war die erste unklar, und zwei
        /// Belastungen für eine Bestellung wären das schlechtere Ergebnis.
        /// </para>
        /// </remarks>
        private static long SyncNumber(string operationId)
            => long.TryParse(operationId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? number
                : throw new TerminalDeviceException(
                    $"The operation id '{operationId}' is not a number, but the local till interface needs a numeric trxSyncNumber.");

        /// <summary>
        /// Der numerische ISO-4217-Code, den das Gerät erwartet.
        /// </summary>
        /// <remarks>
        /// Nur die Währungen, die hier vorkommen. Eine fehlende abzuweisen ist besser als eine zu raten:
        /// ein falscher Code bucht in einer anderen Währung, und das fällt erst in der Abrechnung auf.
        /// </remarks>
        private static int NumericCurrency(string currency)
            => currency?.Trim().ToUpperInvariant() switch
            {
                "CHF" => 756,
                "EUR" => 978,
                "USD" => 840,
                "GBP" => 826,
                _ => throw new TerminalDeviceException(
                    $"No numeric ISO-4217 code is known here for currency '{currency}'. Add it rather than letting the terminal guess — a wrong code charges in a different currency.")
            };

        /// <inheritdoc />
        /// <remarks>
        /// Die Liste folgt <see cref="WalleeLtiTerminalOptions"/> — was dort ein Feld ist, ist hier
        /// eines. Wer das eine ändert, ändert das andere mit, sonst fragt die Maske nach etwas, das
        /// niemand liest, oder lässt weg, was gebraucht wird.
        /// </remarks>
        public Task<IReadOnlyList<TerminalSettingDescriptor>> DescribeSettingsAsync(
            CancellationToken cancellationToken = default)
        {
            var defaults = new WalleeLtiTerminalOptions();
            IReadOnlyList<TerminalSettingDescriptor> fields =
            [
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.Host).ToLowerInvariant(),
                    Label = "Adresse des Terminals",
                    Kind = TerminalSettingKind.Text,
                    Required = true,
                    HelpText = "IP-Adresse oder Name des Geräts im Netz des Ladens. Der Kassen-PC muss es direkt erreichen."
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.Port).ToLowerInvariant(),
                    Label = "Port",
                    Kind = TerminalSettingKind.Number,
                    Required = true,
                    DefaultValue = defaults.Port.ToString(),
                    HelpText = "wallee verwendet 50000, sofern am Gerät nichts anderes eingestellt ist."
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.PosId).ToLowerInvariant(),
                    Label = "Kassenkennung (posId)",
                    Kind = TerminalSettingKind.Text,
                    Required = true,
                    HelpText = "Wie sich diese Kasse gegenüber dem Terminal ausweist."
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.ReceiptFormat).ToLowerInvariant(),
                    Label = "Belegformat",
                    Kind = TerminalSettingKind.Number,
                    DefaultValue = defaults.ReceiptFormat.ToString(),
                    HelpText = "2 ist das übliche Textformat."
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.ShowTransactionResultScreens).ToLowerInvariant(),
                    Label = "Ergebnis am Gerät anzeigen",
                    Kind = TerminalSettingKind.Boolean,
                    DefaultValue = defaults.ShowTransactionResultScreens.ToString()
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.SuppressDynamicCurrencyConversion).ToLowerInvariant(),
                    Label = "Währungsumrechnung am Gerät unterdrücken",
                    Kind = TerminalSettingKind.Boolean,
                    DefaultValue = defaults.SuppressDynamicCurrencyConversion.ToString(),
                    HelpText = "Empfohlen. Mit Umrechnung weicht der belastete Betrag von dem ab, der auf der Verkaufszeile steht."
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.TransactionTimeoutSeconds).ToLowerInvariant(),
                    Label = "Wartezeit je Zahlung (Sekunden)",
                    Kind = TerminalSettingKind.Number,
                    DefaultValue = defaults.TransactionTimeoutSeconds.ToString(),
                    HelpText = "Grosszügig wählen: ein Mensch steckt eine Karte ein und tippt eine PIN. Läuft sie ab, gilt der Ausgang als unklar — nicht als gescheitert."
                },
                new()
                {
                    Name = nameof(WalleeLtiTerminalOptions.ConnectTimeoutSeconds).ToLowerInvariant(),
                    Label = "Wartezeit auf die Verbindung (Sekunden)",
                    Kind = TerminalSettingKind.Number,
                    DefaultValue = defaults.ConnectTimeoutSeconds.ToString()
                }
            ];

            return Task.FromResult(fields);
        }

        /// <summary>
        /// Liest die Gerätekonfiguration aus dem, was mit der Anfrage kam.
        /// </summary>
        /// <remarks>
        /// <b>Die einzige Quelle.</b> Es gibt hier bewusst keinen Rückfall auf etwas Lokales: gäbe es
        /// einen, könnte eine veraltete Angabe am Kassen-PC still gewinnen, und man suchte den Fehler
        /// dort, wo das Gerät gepflegt wird.
        /// <para>
        /// Felder, die diese Klasse nicht kennt, werden übergangen — dasselbe JSON trägt auch, was der
        /// Aufrufer braucht, um den Agenten überhaupt zu erreichen.
        /// </para>
        /// </remarks>
        private static WalleeLtiTerminalOptions Read(TerminalTarget target)
        {
            if (string.IsNullOrWhiteSpace(target.ConfigurationJson))
            {
                throw new TerminalDeviceException(
                    $"No configuration came with the request for terminal '{target.TerminalId}'. This device holds no list of its own — address, port and posId have to travel with every call.");
            }

            WalleeLtiTerminalOptions? options;
            try
            {
                options = JsonSerializer.Deserialize<WalleeLtiTerminalOptions>(target.ConfigurationJson, Json);
            }
            catch (JsonException ex)
            {
                throw new TerminalDeviceException(
                    $"The configuration sent for terminal '{target.TerminalId}' is not readable JSON: {ex.Message}", ex);
            }

            if (options == null || string.IsNullOrWhiteSpace(options.Host))
            {
                throw new TerminalDeviceException(
                    $"The configuration sent for terminal '{target.TerminalId}' names no host. Without an address in the shop's network there is no way to reach the device.");
            }

            return options;
        }

        private static int? ReadInt(XElement parent, string name)
            => int.TryParse(parent.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        private static long? ReadLong(XElement parent, string name)
            => long.TryParse(parent.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        /// <summary>
        /// Sammelt ein, was für den Beleg gebraucht wird.
        /// </summary>
        /// <remarks>
        /// Der gedruckte Text kommt <b>nicht</b> in der Antwort, sondern in einer Benachrichtigung
        /// mittendrin (<c>printerNotification</c>). Wer nur auf die Antwort wartet, hat am Ende eine
        /// erfolgreiche Zahlung und keinen Beleg.
        /// </remarks>
        private sealed class ReceiptCollector
        {
            private string? merchantReceipt;

            public void Collect(XElement notification)
            {
                if (notification.Name.LocalName != "printerNotification")
                {
                    return;
                }

                merchantReceipt ??= notification.Element("merchantReceipt")?.Value.Trim()
                                    ?? notification.Element(WalleeLtiConnection.Device + "merchantReceipt")?.Value.Trim();
            }

            public TerminalReceipt? Build(XElement? body = null)
            {
                if (merchantReceipt == null && body == null)
                {
                    return null;
                }

                return new TerminalReceipt
                {
                    Brand = body?.Element("cardAppLabel")?.Value,
                    MaskedPan = body?.Element("cardNumber")?.Value,
                    AuthorizationCode = body?.Element("ep2AuthCode")?.Value,
                    ApplicationIdentifier = body?.Element("cardAppId")?.Value,
                    ApplicationLabel = body?.Element("cardAppLabel")?.Value,
                    VerificationMethod = body?.Element("cvm")?.Value,
                    TerminalId = body?.Element("ep2TrmId")?.Value,
                    MerchantReference = body?.Element("merchantReference")?.Value,
                    TimestampUtc = ParseTimestamp(body?.Element("transactionTime")?.Value),
                    PreformattedText = merchantReceipt
                };
            }

            /// <summary>Das Gerät schreibt die Zeit als <c>yyyyMMddHHmmss</c>.</summary>
            private static DateTime? ParseTimestamp(string? value)
                => DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var parsed)
                    ? parsed
                    : null;
        }
    }
}
