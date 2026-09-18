namespace ITVComponents.WebCoreToolkit.Billing.Terminals.WalleeLti
{
    /// <summary>
    /// Was der Kassen-Agent über ein wallee-Terminal im lokalen Netz wissen muss.
    /// </summary>
    /// <remarks>
    /// Die Angaben stehen beim AGENTEN und nicht in der Web-Anwendung: eine Adresse im LAN des Ladens
    /// ist von aussen weder erreichbar noch sinnvoll zu verwalten, und der Agent steht ohnehin daneben.
    /// Ein Gerätename in dieser Zuordnung ist dasselbe, was in der Web-Anwendung als
    /// <c>ProviderTerminalId</c> an der Geräte-Zeile steht — darüber finden sich die beiden Seiten.
    /// </remarks>
    public class WalleeLtiOptions
    {
        /// <summary>
        /// Die Geräte dieses Agenten, nach dem Namen, unter dem die Web-Anwendung sie anspricht.
        /// </summary>
        public Dictionary<string, WalleeLtiTerminalOptions> Terminals { get; set; } = new();

        /// <summary>
        /// Wie lange auf das Ende einer Zahlung gewartet wird. Grosszügig, weil ein Mensch eine Karte
        /// einsteckt und eine PIN tippt.
        /// </summary>
        /// <remarks>
        /// Läuft sie ab, gilt der Ausgang als <b>unklar</b> — nicht als gescheitert. Das Terminal kann
        /// die Zahlung längst durchgeführt haben.
        /// </remarks>
        public int TransactionTimeoutSeconds { get; set; } = 180;

        /// <summary>Wie lange auf das Zustandekommen der Verbindung gewartet wird.</summary>
        public int ConnectTimeoutSeconds { get; set; } = 10;
    }

    /// <summary>Ein einzelnes Terminal im lokalen Netz.</summary>
    public class WalleeLtiTerminalOptions
    {
        /// <summary>Die Adresse des Geräts im LAN.</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>Der Port, auf dem das Gerät horcht. wallee verwendet 50000, sofern nichts anderes eingestellt ist.</summary>
        public int Port { get; set; } = 50000;

        /// <summary>
        /// Die Kennung dieser Kasse gegenüber dem Terminal (<c>posId</c>).
        /// </summary>
        public string PosId { get; set; } = string.Empty;

        /// <summary>
        /// Das Belegformat, das das Gerät liefern soll. <c>2</c> ist das übliche Textformat.
        /// </summary>
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
    }
}
