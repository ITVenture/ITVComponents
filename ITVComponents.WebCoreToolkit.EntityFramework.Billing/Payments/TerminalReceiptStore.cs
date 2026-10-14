using System.Text.Json;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Legt die Belegangaben eines Terminals am Verkauf ab und liest sie wieder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Am Verkauf und nicht nur im Rückgabewert: der Beobachter, der beim Host den Beleg bucht, sieht nur den
    /// <see cref="TenantSale"/>. Und ein späteres Nachfragen (nach einem „Später klären“ an der Kasse) fragt das
    /// Gerät nicht mehr — dann ist die Zeile das Einzige, was die Angaben noch kennt.
    /// </para>
    /// <para>
    /// <b>Nie die volle Kartennummer.</b> Die Anbindungen liefern sie maskiert; hier wird das nicht geglaubt,
    /// sondern geprüft. Eine Nummer, die nicht maskiert aussieht, wird verworfen und als Fehler protokolliert —
    /// ein Beleg ohne Nummer ist ärgerlich, eine gespeicherte Kartennummer ist ein Datenschutzvorfall.
    /// </para>
    /// </remarks>
    public static class TerminalReceiptStore
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Übernimmt, was das Gerät gemeldet hat. Bereits Gespeichertes wird nicht durch „nichts“ ersetzt.
        /// </summary>
        /// <returns>true, wenn die Zeile danach einen Beleg trägt</returns>
        public static bool Store(TenantSale sale, TerminalReceipt? receipt, long? tipMinor)
        {
            if (tipMinor is > 0)
            {
                sale.TipMinor = tipMinor;
            }

            if (receipt == null)
            {
                return sale.TerminalReceiptJson != null;
            }

            if (!LooksMasked(receipt.MaskedPan))
            {
                LogEnvironment.LogEvent(
                    $"The terminal reported a card number for sale {sale.TenantSaleId} (tenant {sale.TenantId}) that does not look masked. It is dropped from the stored receipt — a full card number must never be stored. Check the terminal integration.",
                    LogSeverity.Error, LogContext);
                receipt.MaskedPan = null;
            }

            sale.TerminalReceiptJson = JsonSerializer.Serialize(receipt, Json);
            return true;
        }

        /// <summary>Die gespeicherten Belegangaben, oder null.</summary>
        public static TerminalReceipt? Of(TenantSale sale)
        {
            if (string.IsNullOrWhiteSpace(sale.TerminalReceiptJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<TerminalReceipt>(sale.TerminalReceiptJson, Json);
            }
            catch (JsonException ex)
            {
                // Lesen darf einen Verkauf nicht unbrauchbar machen - er ist bezahlt, nur der Beleg fehlt.
                LogEnvironment.LogEvent(
                    $"The stored receipt of sale {sale.TenantSaleId} (tenant {sale.TenantId}) is not readable and is treated as missing: {ex.OutlineException()}",
                    LogSeverity.Error, LogContext);
                return null;
            }
        }

        /// <summary>
        /// Ob eine Kartennummer maskiert aussieht.
        /// </summary>
        /// <remarks>
        /// Eine volle Kartennummer hat 12 bis 19 Stellen; ein Beleg zeigt höchstens die ersten sechs und die
        /// letzten vier. Mehr als zehn Ziffern sind also keine maskierte Nummer, egal was dazwischen steht.
        /// </remarks>
        internal static bool LooksMasked(string? pan)
            => string.IsNullOrWhiteSpace(pan) || pan.Count(char.IsDigit) <= 10;

        private const string LogContext = "TenantPayments";
    }
}
