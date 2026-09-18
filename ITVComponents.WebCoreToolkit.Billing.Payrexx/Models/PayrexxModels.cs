namespace ITVComponents.WebCoreToolkit.Billing.Payrexx.Models
{
    /// <summary>
    /// Die Zahlungsseite, wie Payrexx sie zurueckgibt.
    /// </summary>
    /// <remarks>
    /// Bewusst schmal: hier stehen nur die Felder, die wirklich gebraucht werden. Eine vollstaendige
    /// Abbildung der Antwort waere Arbeit, die bei jeder API-Version nachgezogen werden muesste, ohne dass
    /// irgendjemand die uebrigen Felder liest.
    /// </remarks>
    public sealed class PayrexxGateway
    {
        /// <summary>Die Kennung der Zahlungsseite bei Payrexx.</summary>
        public int Id { get; set; }

        /// <summary>Die Kennung, die in der Zahlungsadresse steht.</summary>
        public string? Hash { get; set; }

        /// <summary>Die Adresse, auf die der Endkunde geschickt wird.</summary>
        public string? Link { get; set; }

        /// <summary>
        /// <c>waiting</c>, <c>confirmed</c>, <c>cancelled</c>, … Roh uebernommen: die Liste gehoert
        /// Payrexx, und sie waechst.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>Die Referenz des Hosts, unveraendert zurueck.</summary>
        public string? ReferenceId { get; set; }

        /// <summary>Der Betrag in der kleinsten Einheit - bei CHF also Rappen.</summary>
        public long Amount { get; set; }

        /// <summary>Die Waehrung als ISO-Code.</summary>
        public string? Currency { get; set; }
    }

    /// <summary>Eine Transaktion - das, was aus einer bezahlten Zahlungsseite wird.</summary>
    public sealed class PayrexxTransaction
    {
        /// <summary>Die Kennung der Transaktion bei Payrexx.</summary>
        public int Id { get; set; }

        /// <summary>Der Zustand, roh uebernommen.</summary>
        public string? Status { get; set; }

        /// <summary>Die Referenz des Hosts, unveraendert zurueck.</summary>
        public string? ReferenceId { get; set; }

        /// <summary>Der Betrag in der kleinsten Einheit.</summary>
        public long Amount { get; set; }

        /// <summary>Die Waehrung als ISO-Code.</summary>
        public string? Currency { get; set; }

        /// <summary>
        /// Die Zahlungsart, mit der tatsaechlich gezahlt wurde (<c>twint</c>, <c>visa</c>, …). Interessant
        /// nicht fuer die Buchung, sondern fuer die Frage, ob sich das Umstellen ueberhaupt lohnt: erst der
        /// Anteil TWINT sagt, wie viel Gebuehr wirklich gespart wird.
        /// </summary>
        public string? PaymentMean { get; set; }
    }
}
