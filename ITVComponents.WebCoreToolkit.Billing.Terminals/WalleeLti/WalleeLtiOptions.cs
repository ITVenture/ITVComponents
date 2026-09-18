namespace ITVComponents.WebCoreToolkit.Billing.Terminals.WalleeLti
{
    /// <summary>
    /// Was in der Gerätekonfiguration steht, damit dieser Weg mit einem Terminal reden kann.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Das ist keine Datei auf dem Kassen-PC.</b> Diese Angaben reisen bei jedem Aufruf als
    /// <see cref="Abstractions.TerminalTarget.ConfigurationJson"/> mit — sie werden in der
    /// Web-Anwendung gepflegt, wo das Gerät ohnehin verwaltet wird.
    /// </para>
    /// <para>
    /// Der Grund ist nicht Bequemlichkeit: eine zweite Pflegestelle am Kassen-PC führte dieselben
    /// Angaben ein zweites Mal, und zwei Wahrheiten über ein Gerät laufen auseinander. Ausserdem wäre
    /// ein frisch aufgesetzter Agent stumm, bis jemand vor Ort war.
    /// </para>
    /// <para>
    /// Das JSON darf mehr enthalten, als hier steht — etwa, über welchen Dienst der Agent zu erreichen
    /// ist. Was diese Klasse nicht kennt, wird beim Lesen übergangen; es geht den Aufrufer an, nicht
    /// das Gerät.
    /// </para>
    /// </remarks>
    public class WalleeLtiTerminalOptions
    {
        /// <summary>Die Adresse des Geräts im lokalen Netz.</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>Der Port, auf dem das Gerät horcht. wallee verwendet 50000, sofern nichts anderes eingestellt ist.</summary>
        public int Port { get; set; } = 50000;

        /// <summary>Die Kennung dieser Kasse gegenüber dem Terminal (<c>posId</c>).</summary>
        public string PosId { get; set; } = string.Empty;

        /// <summary>Das Belegformat, das das Gerät liefern soll. <c>2</c> ist das übliche Textformat.</summary>
        public int ReceiptFormat { get; set; } = 2;

        /// <summary>Ob das Gerät seine Ergebnisbildschirme selbst anzeigt.</summary>
        public bool ShowTransactionResultScreens { get; set; } = true;

        /// <summary>
        /// Ob eine Währungsumrechnung am Gerät (DCC) unterdrückt wird.
        /// </summary>
        /// <remarks>
        /// Vorbelegt mit ja, und das ist die vorsichtige Richtung: mit DCC weicht der belastete Betrag
        /// von dem ab, der auf der Verkaufszeile steht, und die Provision daneben ist auf den
        /// ursprünglichen gerechnet.
        /// </remarks>
        public bool SuppressDynamicCurrencyConversion { get; set; } = true;

        /// <summary>
        /// Wie lange auf das Ende einer Zahlung gewartet wird. Grosszügig, weil ein Mensch eine Karte
        /// einsteckt und eine PIN tippt.
        /// </summary>
        /// <remarks>
        /// Läuft sie ab, gilt der Ausgang als <b>unklar</b> — nicht als gescheitert. Das Gerät kann die
        /// Zahlung längst durchgeführt haben.
        /// </remarks>
        public int TransactionTimeoutSeconds { get; set; } = 180;

        /// <summary>Wie lange auf das Zustandekommen der Verbindung gewartet wird.</summary>
        public int ConnectTimeoutSeconds { get; set; } = 10;
    }
}
