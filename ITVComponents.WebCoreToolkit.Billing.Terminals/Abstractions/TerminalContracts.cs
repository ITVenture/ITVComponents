namespace ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions
{
    /// <summary>Der Auftrag, am Gerät zu kassieren.</summary>
    public class TerminalPaymentCommand
    {
        /// <summary>Das Gerät, gemeint ist seine Kennung beim Anbieter bzw. auf dem Agenten.</summary>
        public string TerminalId { get; set; } = string.Empty;

        /// <summary>
        /// Die Kennung dieses Vorgangs — <b>von uns</b> vergeben, nicht vom Gerät.
        /// </summary>
        /// <remarks>
        /// Sie ist das, was einen Wiederholungsversuch von einer zweiten Belastung unterscheidet. Wer
        /// denselben Auftrag zweimal bekommt, gibt den Stand des ersten zurück. Bei wallee heisst das
        /// Gegenstück <c>trxSyncNumber</c>, bei Stripe ist es der wiederverwendete PaymentIntent — jeder
        /// dieser Anbieter hat einen solchen Wert, weil jeder dieses Problem hat.
        /// </remarks>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>Der Betrag in der kleinsten Einheit (Rappen), wie überall in diesem Umfeld.</summary>
        public long AmountMinor { get; set; }

        /// <summary>Die Währung als ISO-Code.</summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>Was auf dem Gerät und auf dem Beleg steht.</summary>
        public string? Description { get; set; }

        /// <summary>Die Bestellnummer des Hosts, soweit das Gerät sie führen kann.</summary>
        public string? Reference { get; set; }

        /// <summary>
        /// Ob der Kunde am Gerät abbrechen darf. Aus dem Ablauf heraus nicht immer erwünscht — an einer
        /// Selbstbedienungskasse ja, bei einer bedienten Kasse eher nicht.
        /// </summary>
        public bool AllowCustomerCancellation { get; set; } = true;

        /// <summary>
        /// Was nur die konkrete Ausprägung versteht, als JSON — die Konfiguration des Geräts.
        /// </summary>
        public string? ConfigurationJson { get; set; }
    }

    /// <summary>Der Auftrag, am Gerät zu erstatten.</summary>
    public class TerminalRefundCommand
    {
        /// <summary>Das Gerät.</summary>
        public string TerminalId { get; set; } = string.Empty;

        /// <summary>Die Kennung dieser Erstattung — dieselbe Rolle wie beim Kassieren.</summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>Der Vorgang, der erstattet wird.</summary>
        public string OriginalOperationId { get; set; } = string.Empty;

        /// <summary>Die Kennung der Zahlung beim Anbieter, soweit bekannt.</summary>
        public string? ProviderPaymentId { get; set; }

        /// <summary>Der zu erstattende Betrag in der kleinsten Einheit.</summary>
        public long AmountMinor { get; set; }

        /// <summary>Die Währung als ISO-Code.</summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>Der Grund, soweit das Gerät ihn führt.</summary>
        public string? Reason { get; set; }

        /// <summary>Was nur die konkrete Ausprägung versteht, als JSON.</summary>
        public string? ConfigurationJson { get; set; }
    }

    /// <summary>Wie ein Vorgang am Gerät ausgegangen ist — oder eben noch nicht.</summary>
    public class TerminalPaymentOutcome
    {
        /// <summary>Die Vorgangskennung, mit der gefragt wurde.</summary>
        public string OperationId { get; set; } = string.Empty;

        /// <summary>Der Stand.</summary>
        public TerminalPaymentState State { get; set; }

        /// <summary>
        /// Die Kennung der Zahlung beim Anbieter — <b>die, gegen die später erstattet wird</b>.
        /// </summary>
        public string? ProviderPaymentId { get; set; }

        /// <summary>Was tatsächlich belastet wurde, in der kleinsten Einheit. Kann vom Auftrag abweichen (Trinkgeld).</summary>
        public long? AmountMinor { get; set; }

        /// <summary>Ein Trinkgeld, falls das Gerät eines erfasst hat.</summary>
        public long? TipMinor { get; set; }

        /// <summary>Der Grund des Scheiterns, roh vom Anbieter.</summary>
        public string? FailureCode { get; set; }

        /// <summary>Die Meldung des Anbieters. Zur Anzeige, nicht für Fallunterscheidungen.</summary>
        public string? FailureMessage { get; set; }

        /// <summary>Was auf den Beleg gehört. Null, wenn noch nichts feststeht.</summary>
        public TerminalReceipt? Receipt { get; set; }
    }

    /// <summary>Der Stand eines Vorgangs am Gerät.</summary>
    public enum TerminalPaymentState
    {
        /// <summary>
        /// <b>Nicht bekannt.</b> Der Vorgang kann gelaufen sein oder nicht.
        /// </summary>
        /// <remarks>
        /// Der wichtigste Wert dieser Aufzählung, und der Grund, warum sie kein <c>bool</c> ist. Er
        /// entsteht, wenn eine Antwort ausbleibt — Stripe beschreibt den falsch negativen
        /// <c>terminal_reader_timeout</c> ausdrücklich: der Fehler kommt zurück, das Gerät hat den Befehl
        /// aber bekommen. Die einzige richtige Reaktion ist <b>nachfragen</b>, niemals neu kassieren.
        /// </remarks>
        Unknown = 0,

        /// <summary>Das Gerät hat den Auftrag, der Kunde ist noch dabei.</summary>
        InProgress = 1,

        /// <summary>Autorisiert, aber noch nicht eingezogen (zweistufiger Ablauf).</summary>
        Authorized = 2,

        /// <summary>Bezahlt.</summary>
        Succeeded = 3,

        /// <summary>Abgelehnt oder fehlgeschlagen.</summary>
        Failed = 4,

        /// <summary>Abgebrochen — vom Kassierer oder vom Kunden am Gerät.</summary>
        Canceled = 5
    }

    /// <summary>
    /// Was der Kartenbeleg braucht.
    /// </summary>
    /// <remarks>
    /// Bewusst hier und nicht erst in der Web-Anwendung: gedruckt wird auf dem Kassen-PC, und diese
    /// Angaben entstehen im selben Vorgang. Sie später ein zweites Mal zu holen, hiesse einen Beleg aus
    /// Daten zu bauen, die inzwischen ein anderer Vorgang sein können.
    /// </remarks>
    public class TerminalReceipt
    {
        /// <summary>Die Kartenmarke.</summary>
        public string? Brand { get; set; }

        /// <summary>Die maskierte Kartennummer.</summary>
        public string? MaskedPan { get; set; }

        /// <summary>Die Genehmigungsnummer der Bank.</summary>
        public string? AuthorizationCode { get; set; }

        /// <summary>Die Kennung der Anwendung auf der Karte (EMV AID) — auf Kartenbelegen vorgeschrieben.</summary>
        public string? ApplicationIdentifier { get; set; }

        /// <summary>Der Name der Kartenanwendung.</summary>
        public string? ApplicationLabel { get; set; }

        /// <summary>Wie bestätigt wurde (PIN, Unterschrift, keine).</summary>
        public string? VerificationMethod { get; set; }

        /// <summary>Die Händlerkennung beim Abwickler.</summary>
        public string? MerchantId { get; set; }

        /// <summary>Die Terminalkennung, wie sie auf den Beleg gehört.</summary>
        public string? TerminalId { get; set; }

        /// <summary>
        /// Die Referenz, die beim Start mitgegeben wurde, soweit der Anbieter sie zurückgibt.
        /// </summary>
        /// <remarks>
        /// Nicht nur Zierde auf dem Beleg: bei Geräten, die nur nach der LETZTEN Transaktion gefragt
        /// werden können, ist sie das einzige Merkmal, an dem sich erkennen lässt, ob die Antwort
        /// überhaupt den Vorgang meint, nach dem gefragt wurde.
        /// </remarks>
        public string? MerchantReference { get; set; }

        /// <summary>Wann die Zahlung stattfand.</summary>
        public DateTime? TimestampUtc { get; set; }

        /// <summary>
        /// Ein fertiger Belegtext, wenn der Anbieter einen liefert. Dann ist er dem Zusammenbauen aus den
        /// Feldern oben vorzuziehen — was vorgeschrieben ist, weiss der Abwickler besser als wir.
        /// </summary>
        public string? PreformattedText { get; set; }
    }

    /// <summary>Ob ein Gerät ansprechbar ist.</summary>
    public class TerminalStatus
    {
        /// <summary>Die Kennung des Geräts.</summary>
        public string TerminalId { get; set; } = string.Empty;

        /// <summary>Ob der Anbieter bzw. der Agent es gerade erreicht.</summary>
        public bool Online { get; set; }

        /// <summary>Ob es gerade einen anderen Vorgang bearbeitet.</summary>
        public bool Busy { get; set; }

        /// <summary>Der Zustand in den Worten des Anbieters.</summary>
        public string? RawState { get; set; }

        /// <summary>Der Anzeigename, soweit der Anbieter einen führt.</summary>
        public string? Label { get; set; }
    }

    /// <summary>Das Gerät oder der Weg dorthin hat nicht mitgespielt.</summary>
    public class TerminalDeviceException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="TerminalDeviceException"/> class.</summary>
        public TerminalDeviceException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="TerminalDeviceException"/> class.</summary>
        public TerminalDeviceException(string message, Exception innerException) : base(message, innerException) { }
    }
}
