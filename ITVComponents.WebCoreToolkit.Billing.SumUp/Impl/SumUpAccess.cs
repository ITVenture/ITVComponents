using ITVComponents.WebCoreToolkit.Billing.SumUp.Models;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;

namespace ITVComponents.WebCoreToolkit.Billing.SumUp.Impl
{
    /// <summary>Was ein Aufruf bei SumUp braucht: den Zugang des Mandanten und seinen Haendlercode.</summary>
    internal readonly record struct SumUpConnection(ProviderCredential Credential, string MerchantCode)
    {
        /// <summary>Kein Geheimnis in einer Protokollzeile, auch nicht versehentlich.</summary>
        public override string ToString() => $"SumUp merchant {MerchantCode}";
    }

    /// <summary>Gemeinsames der beiden SumUp-Dienste.</summary>
    internal static class SumUpAccess
    {
        /// <summary>Der Name, unter dem dieser Anbieter angesprochen wird.</summary>
        public const string Key = "sumup";

        /// <summary>Holt den Zugang des Mandanten, ueber den dieser Vorgang laeuft.</summary>
        /// <remarks>
        /// SumUp kennt keinen Plattform-Zugang - das haelt schon die Basis fest (<c>RequiresOwnAccess</c>). Die
        /// Pruefung hier ist die zweite Sicherung an der Stelle, an der das Geheimnis geholt wird.
        /// </remarks>
        public static async Task<SumUpConnection> ConnectAsync(ProviderAccessSelection access,
            IProviderCredentialStore credentials, CancellationToken cancellationToken)
        {
            if (!access.IsOwnAccess || string.IsNullOrWhiteSpace(access.ConnectionName)
                                    || string.IsNullOrWhiteSpace(access.ProviderAccountId))
            {
                throw new TenantPaymentException(PaymentErrorCodes.OwnAccessUnavailable,
                    $"SumUp needs the tenant's own access with a merchant code; {access} has none.");
            }

            var credential = await credentials.GetCredentialAsync(access.TenantId, access.ConnectionName, cancellationToken);
            return new SumUpConnection(credential, access.ProviderAccountId);
        }

        /// <summary>Die Pfade der Geraete-API (v0.1).</summary>
        public static string Readers(string merchantCode) => $"v0.1/merchants/{Uri.EscapeDataString(merchantCode)}/readers";

        public static string Reader(string merchantCode, string readerId) => $"{Readers(merchantCode)}/{Uri.EscapeDataString(readerId)}";

        /// <summary>Die Transaktions-API (v2.1); <paramref name="query"/> ist schon kodiert.</summary>
        public static string Transaction(string merchantCode, string query)
            => $"v2.1/merchants/{Uri.EscapeDataString(merchantCode)}/transactions?{query}";

        /// <summary>
        /// Die Zahl der Nachkommastellen einer Waehrung - SumUp will sie im Geraete-Checkout ausdruecklich.
        /// </summary>
        public static int MinorUnitOf(string currency)
        {
            var factor = CurrencyMinorUnits.Factor(currency);
            var digits = 0;
            while (factor > 1)
            {
                factor /= 10;
                digits++;
            }

            return digits;
        }

        /// <summary>
        /// Baut den Kartenbeleg aus Transaktion und Belegdaten.
        /// </summary>
        /// <remarks>
        /// SumUp liefert die Daten zerlegt und keinen fertigen Text - <see cref="TerminalReceipt.PreformattedText"/>
        /// bleibt darum leer und der Host setzt den Beleg aus den Feldern. Fehlt der Beleg-Abruf, entsteht er aus
        /// der Transaktion allein: ein Verkauf ohne Beleg waere der schlechtere Ausgang.
        /// </remarks>
        public static TerminalReceipt BuildReceipt(SumUpTransaction transaction, SumUpReceipt? receipt,
            string merchantCode, string? reference)
        {
            var card = receipt?.TransactionData?.Card ?? transaction.Card;
            return new TerminalReceipt
            {
                Brand = card?.Type,
                // Nur die letzten vier Ziffern, mit Sternen davor: alles andere verwirft die Ablage ohnehin, und
                // eine volle Nummer gehoert nicht auf einen Beleg.
                MaskedPan = string.IsNullOrWhiteSpace(card?.Last4Digits) ? null : $"**** **** **** {card.Last4Digits}",
                AuthorizationCode = receipt?.AcquirerData?.AuthorizationCode,
                TerminalId = receipt?.AcquirerData?.TerminalId,
                VerificationMethod = receipt?.TransactionData?.VerificationMethod,
                ApplicationIdentifier = ApplicationIdOf(receipt),
                MerchantId = merchantCode,
                MerchantReference = reference,
                TimestampUtc = transaction.Timestamp?.ToUniversalTime()
            };
        }

        /// <summary>Die AID aus den EMV-Daten, soweit sie unter einem der ueblichen Namen steht.</summary>
        private static string? ApplicationIdOf(SumUpReceipt? receipt)
        {
            if (receipt?.EmvData is not { ValueKind: System.Text.Json.JsonValueKind.Object } emv)
            {
                return null;
            }

            foreach (var name in new[] { "aid", "application_identifier", "AID" })
            {
                if (emv.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return null;
        }
    }
}
