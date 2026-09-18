using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Models
{
    /// <summary>Die Hülle, in der Payrexx eine Benachrichtigung schickt.</summary>
    public sealed class PayrexxWebhookNotification
    {
        /// <summary>Die Transaktion, um die es geht. Fehlt bei anderen Ereignisarten (z.B. Auszahlungen).</summary>
        public PayrexxWebhookTransaction? Transaction { get; set; }
    }

    /// <summary>
    /// Die Transaktion aus einer Benachrichtigung — bewusst nur die Felder, aus denen etwas gebucht wird.
    /// </summary>
    /// <remarks>
    /// Nicht mit <see cref="PayrexxTransaction"/> (Händler-API) oder
    /// <see cref="PayrexxServiceTransaction"/> (Service-API) zu verwechseln: dieselbe Sache, drei
    /// Blickwinkel, und die Felder decken sich nur teilweise.
    /// </remarks>
    public sealed class PayrexxWebhookTransaction
    {
        /// <summary>Die laufende Nummer.</summary>
        public long Id { get; set; }

        /// <summary>
        /// Die Kennung, mit der sich die Transaktion adressieren lässt — <b>die für die Erstattung</b>.
        /// Die Nummer oben taugt dafür nicht.
        /// </summary>
        public string? Uuid { get; set; }

        /// <summary>
        /// Der Zustand: <c>confirmed</c>, <c>cancelled</c>, <c>declined</c>, <c>expired</c>,
        /// <c>refunded</c>, <c>partially-refunded</c>, <c>chargeback</c>, … Roh übernommen, weil die
        /// Liste Payrexx gehört und wächst.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>Unsere Referenz, unverändert zurück.</summary>
        public string? ReferenceId { get; set; }

        /// <summary>Der Betrag in der kleinsten Einheit.</summary>
        public long Amount { get; set; }

        /// <summary>Die Währung als ISO-Code.</summary>
        public string? Currency { get; set; }

        /// <summary>Womit tatsächlich gezahlt wurde (<c>twint</c>, <c>visa</c>, …).</summary>
        public string? PaymentMean { get; set; }

        /// <summary>
        /// Die Transaktion, auf die sich diese hier bezieht — gesetzt, wenn dies <b>selbst eine
        /// Erstattung</b> ist.
        /// </summary>
        /// <remarks>
        /// <b>Der Grund, warum dieses Feld gelesen wird:</b> Payrexx führt eine Erstattung als eigene
        /// Transaktion mit eigener UUID. Wer die blind in <c>ProviderChargeId</c> schreibt, richtet die
        /// nächste Erstattung gegen die vorige statt gegen die Zahlung — und das fällt erst auf, wenn
        /// eine Rückzahlung nicht ankommt. Steht hier etwas, ist DAS die Kennung der Zahlung.
        /// </remarks>
        public string? OriginalTransactionUuid { get; set; }

        /// <summary>
        /// Dieselbe Rolle wie <see cref="OriginalTransactionUuid"/>. Payrexx führt beide Namen; welcher
        /// gefüllt ist, hängt an der Art des Vorgangs, und keiner der beiden ist verlässlich der einzige.
        /// </summary>
        public string? SourceTransactionUuid { get; set; }

        /// <summary>
        /// Die Rechnung — hier steht die Verbindung zur Zahlungsseite und der insgesamt erstattete Betrag.
        /// </summary>
        public PayrexxWebhookInvoice? Invoice { get; set; }

        /// <summary>Der Zahlende, soweit erfasst.</summary>
        public PayrexxWebhookContact? Contact { get; set; }
    }

    /// <summary>Der Rechnungsteil einer Transaktion.</summary>
    public sealed class PayrexxWebhookInvoice
    {
        /// <summary>
        /// Die Kennung der Zahlungsseite, aus der diese Transaktion entstanden ist — <b>der verlässliche
        /// Weg zurück zur Verkaufszeile</b>, weil genau diese Zahl beim Anlegen gespeichert wurde.
        /// </summary>
        [JsonPropertyName("paymentRequestId")]
        public long? PaymentRequestId { get; set; }

        /// <summary>Unsere Referenz, auch hier nochmals.</summary>
        public string? ReferenceId { get; set; }

        /// <summary>
        /// Was insgesamt erstattet wurde, in der kleinsten Einheit. Payrexx meldet keine einzelne
        /// Erstattung, sondern nur diesen Stand — die Differenz zieht
        /// <see cref="EntityFramework.Billing.Payments.TenantSaleWebhookSink{TContext}.MirrorRefundTotalAsync"/>.
        /// </summary>
        public long? RefundedAmount { get; set; }

        /// <summary>Der ursprüngliche Betrag, in der kleinsten Einheit.</summary>
        public long? OriginalAmount { get; set; }
    }

    /// <summary>Der Zahlende.</summary>
    public sealed class PayrexxWebhookContact
    {
        /// <summary>
        /// Die Mailadresse. Wird nur übernommen, wenn auf der Zeile noch keine steht — und landet
        /// <b>nie</b> im Protokoll.
        /// </summary>
        public string? Email { get; set; }
    }
}
