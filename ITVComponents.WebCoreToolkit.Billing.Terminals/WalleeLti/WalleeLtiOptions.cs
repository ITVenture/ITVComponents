using ITVComponents.AgentServices.Abstractions;

namespace ITVComponents.WebCoreToolkit.Billing.Terminals.WalleeLti
{
    /// <summary>
    /// Was ein wallee-Terminal am Agenten braucht, damit der Agent mit ihm reden kann.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Liegt am Agenten.</b> Eingerichtet wird einmal über die Web-Anwendung (die Maske entsteht aus den
    /// <see cref="DeviceSettingAttribute"/>n hier), abgelegt wird lokal, und das Gerät bekommt die Werte im
    /// Konstruktor. Die Aufrufe tragen danach nur noch das Was — ein Aufruf, der unterwegs verloren geht,
    /// kann keinen halben Konfigurationsstand hinterlassen.
    /// </para>
    /// <para>
    /// Was hier kein <see cref="DeviceSettingAttribute"/> trägt, ist kein Feld und lässt sich von aussen
    /// nicht setzen.
    /// </para>
    /// </remarks>
    public class WalleeLtiTerminalOptions
    {
        /// <summary>Die Adresse des Geräts im lokalen Netz.</summary>
        [DeviceSetting("Adresse des Terminals", Required = true,
            HelpText = "IP-Adresse oder Name des Geräts im Netz des Ladens. Der Kassen-PC muss es direkt erreichen.")]
        public string Host { get; set; } = string.Empty;

        /// <summary>Der Port, auf dem das Gerät horcht. wallee verwendet 50000, sofern nichts anderes eingestellt ist.</summary>
        [DeviceSetting("Port", Required = true,
            HelpText = "wallee verwendet 50000, sofern am Gerät nichts anderes eingestellt ist.")]
        public int Port { get; set; } = 50000;

        /// <summary>Die Kennung dieser Kasse gegenüber dem Terminal (<c>posId</c>).</summary>
        [DeviceSetting("Kassenkennung (posId)", Required = true,
            HelpText = "Wie sich diese Kasse gegenüber dem Terminal ausweist.")]
        public string PosId { get; set; } = string.Empty;

        /// <summary>Das Belegformat, das das Gerät liefern soll. <c>2</c> ist das übliche Textformat.</summary>
        [DeviceSetting("Belegformat", HelpText = "2 ist das übliche Textformat.")]
        public int ReceiptFormat { get; set; } = 2;

        /// <summary>Ob das Gerät seine Ergebnisbildschirme selbst anzeigt.</summary>
        [DeviceSetting("Ergebnis am Gerät anzeigen")]
        public bool ShowTransactionResultScreens { get; set; } = true;

        /// <summary>
        /// Ob eine Währungsumrechnung am Gerät (DCC) unterdrückt wird.
        /// </summary>
        /// <remarks>
        /// Vorbelegt mit ja, und das ist die vorsichtige Richtung: mit DCC weicht der belastete Betrag
        /// von dem ab, der auf der Verkaufszeile steht, und die Provision daneben ist auf den
        /// ursprünglichen gerechnet.
        /// </remarks>
        [DeviceSetting("Währungsumrechnung am Gerät unterdrücken",
            HelpText = "Empfohlen. Mit Umrechnung weicht der belastete Betrag von dem ab, der auf der Verkaufszeile steht.")]
        public bool SuppressDynamicCurrencyConversion { get; set; } = true;

        /// <summary>
        /// Wie lange auf das Ende einer Zahlung gewartet wird. Grosszügig, weil ein Mensch eine Karte
        /// einsteckt und eine PIN tippt.
        /// </summary>
        /// <remarks>
        /// Läuft sie ab, gilt der Ausgang als <b>unklar</b> — nicht als gescheitert. Das Gerät kann die
        /// Zahlung längst durchgeführt haben.
        /// </remarks>
        [DeviceSetting("Wartezeit je Zahlung (Sekunden)",
            HelpText = "Grosszügig wählen: ein Mensch steckt eine Karte ein und tippt eine PIN. Läuft sie ab, gilt der Ausgang als unklar — nicht als gescheitert.")]
        public int TransactionTimeoutSeconds { get; set; } = 180;

        /// <summary>Wie lange auf das Zustandekommen der Verbindung gewartet wird.</summary>
        [DeviceSetting("Wartezeit auf die Verbindung (Sekunden)")]
        public int ConnectTimeoutSeconds { get; set; } = 10;
    }
}
