namespace ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions
{
    /// <summary>
    /// Ein Zahlungsterminal, so wie es von der Stelle aus aussieht, die es <b>direkt</b> anspricht.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der geräteseitige Vertrag — implementiert auf dem Kassen-Agenten, gerufen über einen Proxy aus der
    /// Web-Anwendung. Er weiss <b>nichts</b> von Mandanten, Verkäufen, Provision oder Datenbank: dort, wo
    /// er läuft, gibt es das alles nicht. Er kennt einen Betrag, ein Gerät und eine Vorgangskennung.
    /// </para>
    /// <para>
    /// <b>Warum das nicht derselbe Vertrag ist wie der web-seitige:</b> die Buchführung darf nicht auf dem
    /// Kassen-PC liegen. Er ist das Gerät, das ausfällt — und der Fall, der Geld kostet, ist genau der, in
    /// dem er mitten in einer Zahlung neu startet. Was dann gilt, muss anderswo stehen.
    /// </para>
    /// <para>
    /// <b>Jede Methode ist wiederholbar.</b> Der Aufrufer weiss in dem Moment, in dem es darauf ankommt,
    /// nicht, ob sein vorheriger Aufruf angekommen ist. Wer zweimal dieselbe
    /// <see cref="TerminalPaymentCommand.OperationId"/> bekommt, darf <b>nicht</b> ein zweites Mal
    /// belasten, sondern muss den Stand des ersten Vorgangs zurückgeben.
    /// </para>
    /// <para>
    /// <b>Jede Methode bekommt das Ziel mitgeliefert</b> (<see cref="TerminalTarget"/>), auch das
    /// Nachfragen und das Abbrechen — die Kennung, unter der der Vorgang geführt wird.
    /// </para>
    /// <para>
    /// <b>Die Einstellungen dagegen liegen beim Gerät.</b> Ein Gerät, das welche braucht (Adresse, Port),
    /// deklariert sie über <c>IHasDeviceSettings&lt;T&gt;</c> und bekommt sie im Konstruktor
    /// (<c>$DeviceSettings</c>). Eingerichtet wird es einmal über den Katalog des Agenten; danach ist es
    /// eine Blackbox, und die Aufrufe tragen nur das Was. <see cref="TerminalTarget.ConfigurationJson"/>
    /// bleibt auf dem Agenten-Weg leer.
    /// </para>
    /// </remarks>
    public interface ITerminalDevice
    {
        /// <summary>
        /// Startet eine Zahlung am Gerät.
        /// </summary>
        /// <remarks>
        /// Kehrt zurück, sobald das Gerät den Auftrag angenommen hat — <b>nicht</b>, wenn der Kunde bezahlt
        /// hat. Das dauert, weil ein Mensch eine Karte einsteckt und eine PIN tippt, und eine Verbindung,
        /// die so lange offen stehen muss, bricht irgendwann im falschen Moment.
        /// </remarks>
        Task<TerminalPaymentOutcome> StartPaymentAsync(TerminalTarget target, TerminalPaymentCommand command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fragt den Stand eines Vorgangs nach.
        /// </summary>
        /// <remarks>
        /// <b>Der wichtigste Aufruf des ganzen Vertrags.</b> Er beantwortet die Frage, die nach einem
        /// Netzabbruch oder einem Neustart offen ist: wurde die Karte belastet? Wer sie nicht beantworten
        /// kann, kassiert doppelt.
        /// </remarks>
        Task<TerminalPaymentOutcome> GetPaymentAsync(TerminalTarget target, string operationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bricht einen laufenden Vorgang ab.
        /// </summary>
        /// <remarks>
        /// Darf scheitern, und das ist kein Fehler: liegt die Karte schon auf und läuft die Autorisierung,
        /// ist es zu spät. Dann sagt das Ergebnis, dass weiter gewartet wird — und nicht, dass abgebrochen
        /// wurde.
        /// </remarks>
        Task<TerminalPaymentOutcome> CancelPaymentAsync(TerminalTarget target, string operationId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Erstattet eine abgeschlossene Zahlung am Gerät, soweit das Gerät das kann.
        /// </summary>
        /// <remarks>
        /// Nicht jedes kann es — dann meldet die Ausprägung das, statt es stillschweigend zu übergehen.
        /// Eine Erstattung, die niemand ausführt und über die niemand spricht, ist die teuerste Variante.
        /// </remarks>
        Task<TerminalPaymentOutcome> RefundPaymentAsync(TerminalTarget target, TerminalRefundCommand command,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Meldet, ob das Gerät erreichbar und bereit ist.
        /// </summary>
        Task<TerminalStatus> GetStatusAsync(TerminalTarget target, CancellationToken cancellationToken = default);
    }
}
