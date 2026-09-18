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

    /// <summary>
    /// Eine Transaktion, wie sie die <b>Service-API</b> zurueckgibt - nicht zu verwechseln mit
    /// <see cref="PayrexxTransaction"/> aus der Haendler-API.
    /// </summary>
    /// <remarks>
    /// Der Unterschied, der zaehlt: hier gibt es eine <see cref="Uuid"/>, und NUR mit der laesst sich eine
    /// Erstattung adressieren. Die Zahl in <see cref="Id"/> sieht aus, als taete sie es auch - sie findet
    /// nur nichts. Wer den Verkauf mit der Zahl statt der UUID verknuepft, merkt das erst beim ersten
    /// Storno.
    /// </remarks>
    public sealed class PayrexxServiceTransaction
    {
        /// <summary>Die laufende Nummer - zum Adressieren NICHT geeignet.</summary>
        public int Id { get; set; }

        /// <summary>Die Kennung, mit der die Transaktion adressiert wird.</summary>
        public string? Uuid { get; set; }

        /// <summary>Der Zustand, etwa <c>partially-refunded</c>. Roh uebernommen.</summary>
        public string? Status { get; set; }

        /// <summary>Die Referenz des Hosts, unveraendert zurueck.</summary>
        public string? ReferenceId { get; set; }

        /// <summary>Der Betrag in der kleinsten Einheit.</summary>
        public long Amount { get; set; }
    }

    /// <summary>Ein Haendler der Plattform, wie ihn die Service-API fuehrt.</summary>
    public sealed class PayrexxMerchant
    {
        /// <summary>Die Kennung des Haendlers bei Payrexx.</summary>
        public int Id { get; set; }

        /// <summary>Der Name der Instanz - der erste Teil von <c>name.payrexx.com</c>.</summary>
        public string? Subdomain { get; set; }

        /// <summary>Die Adresse des Verwalter-Kontos.</summary>
        public string? Email { get; set; }

        /// <summary>Ob der Haendler aktiv ist.</summary>
        public bool Active { get; set; }

        /// <summary>
        /// Ob der Haendler eingeschraenkt gefuehrt wird - im Marktplatz-Modell die Vorgabe.
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>Unsere eigene Kennung, die beim Anlegen mitgegeben wurde.</summary>
        public string? Reference { get; set; }

        /// <summary>
        /// Das frisch erzeugte Konto. <b>Enthaelt ein Passwort</b> - es darf nirgends ins Protokoll und
        /// nirgends in die Datenbank.
        /// </summary>
        public PayrexxMerchantAccount? Account { get; set; }
    }

    /// <summary>Das Verwalter-Konto, das beim Anlegen eines Haendlers entstehen kann.</summary>
    public sealed class PayrexxMerchantAccount
    {
        /// <summary>Ob ueberhaupt ein neues Konto erzeugt wurde.</summary>
        public bool Created { get; set; }

        /// <summary>
        /// Das erzeugte Passwort - nur gesetzt, wenn <see cref="Created"/> gilt.
        /// <b>Nicht protokollieren, nicht speichern.</b> Es geht den Haendler an, nicht uns.
        /// </summary>
        public string? Password { get; set; }
    }

    /// <summary>Der Stand der Identitaetspruefung (KYC) eines Haendlers.</summary>
    /// <remarks>
    /// Liegt bei Payrexx unter Version <b>v2.0</b>, waehrend der Haendler selbst unter v2.3 steht.
    /// </remarks>
    public sealed class PayrexxVerification
    {
        /// <summary>
        /// Der Stand, etwa <c>approved</c>. Roh uebernommen: die Liste gehoert Payrexx, und sie waechst -
        /// eine eigene Aufzaehlung waere in dem Moment falsch, in dem ein Wert dazukommt.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>Ob die Unterlagen eingereicht sind (<c>PROVIDED</c>).</summary>
        public string? VerificationDocument { get; set; }
    }
}
