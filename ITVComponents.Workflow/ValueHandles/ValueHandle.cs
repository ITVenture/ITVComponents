using System;
using ITVComponents.Logging;

namespace ITVComponents.Workflow.ValueHandles
{
    /// <summary>
    /// Der Griff auf einen Wert, der nicht im Variablen-Stack liegt: lesen, aendern, zurueckschreiben.
    /// </summary>
    /// <remarks>
    /// Gebaut wird der Griff <b>immer von der Engine</b>, nach einem erfolgreichen
    /// <see cref="IWorkflowValueHandler.Read"/>. Er lebt genau so lange wie die Aufloesung, in der er
    /// entstanden ist: er haelt ein lebendes Objekt und einen Handler aus dem Scope der laufenden
    /// Arbeitseinheit und ueberlebt weder das Parken einer Aufgabe noch einen Blazor-Reconnect. Deshalb
    /// gehoert er weder in den Variablen-Stack noch in einen Verlaufs-Schnappschuss.
    /// </remarks>
    public sealed class ValueHandle
    {
        /// <summary>
        /// der Handler, der liest und schreibt
        /// </summary>
        private readonly IWorkflowValueHandler handler;

        /// <summary>
        /// wird nach jedem Schreibversuch gerufen - so landet der Vorgang im Verlauf der Instanz
        /// </summary>
        private readonly Action<ValueHandle, Exception> written;

        /// <summary>
        /// Initialisiert einen Griff auf einen bereits gelesenen Wert.
        /// </summary>
        /// <param name="handler">der Handler, der liest und schreibt</param>
        /// <param name="request">die Anfrage, mit der gelesen wurde</param>
        /// <param name="value">der gelesene Wert</param>
        /// <param name="written">wird nach jedem Schreibversuch gerufen (Fehler als zweites Argument), oder null</param>
        internal ValueHandle(IWorkflowValueHandler handler, ValueHandleRequest request, object value,
            Action<ValueHandle, Exception> written)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            this.written = written;
            Value = value;
        }

        /// <summary>
        /// Der Wert. Aendern heisst hier nur: das Backing-Feld aendern - geschrieben wird erst mit
        /// <see cref="WriteBack"/>.
        /// </summary>
        /// <remarks>
        /// Bei einem <b>Referenztyp</b> ist genau das der Gewinn: eine Aktivitaet, die das Objekt
        /// mutiert, mutiert dasselbe Objekt, das hier liegt - sie muss von diesem Mechanismus nichts
        /// wissen.
        /// </remarks>
        public object Value { get; set; }

        /// <summary>Die Anfrage, mit der gelesen wurde und mit der geschrieben wird.</summary>
        public ValueHandleRequest Request { get; }

        /// <summary>Ob bereits (mindestens einmal) erfolgreich zurueckgeschrieben wurde.</summary>
        public bool Written { get; private set; }

        /// <summary>Wie oft erfolgreich zurueckgeschrieben wurde.</summary>
        public int WriteCount { get; private set; }

        /// <summary>Ob die Runde, in der dieser Griff entstanden ist, vorbei ist.</summary>
        public bool Closed { get; private set; }

        /// <summary>
        /// Schliesst den Griff - gerufen von der Runde, wenn sie endet.
        /// </summary>
        /// <remarks>
        /// Der Handler stammt aus dem Scope der Arbeitseinheit und lebt danach weiter. Ohne diesen Riegel
        /// wuerde ein festgehaltener Griff also klaglos weiterschreiben, obwohl die Runde vorbei ist -
        /// ausserhalb jeder Aufloesung, und damit ausserhalb dessen, was der Verlauf der Instanz
        /// nachvollziehbar macht.
        /// </remarks>
        internal void Close()
        {
            Closed = true;
        }

        /// <summary>
        /// Schreibt <see cref="Value"/> ueber den Handler zurueck.
        /// </summary>
        /// <remarks>
        /// Zweimaliges Schreiben ist erlaubt - was es bedeutet, entscheidet der Handler -, wird aber
        /// protokolliert: wer es tut, soll es sehen.
        /// </remarks>
        public void WriteBack()
        {
            if (Closed)
            {
                throw new InvalidOperationException(
                    $"Value handle {Request} is written back after its resolution round has ended. The "
                    + "handle only lives as long as the round that built it - a task that was parked in "
                    + "the meantime resolves again on completion, and that is the handle to write.");
            }

            if (Written)
            {
                LogEnvironment.LogEvent(
                    $"Value handle {Request} is written back again (write #{WriteCount + 1}). What that " +
                    "means is up to the handler - but it is nothing that happens by accident.",
                    LogSeverity.Warning);
            }

            try
            {
                handler.Write(Request, Value);
            }
            catch (Exception ex)
            {
                written?.Invoke(this, ex);
                throw;
            }

            Written = true;
            WriteCount++;
            written?.Invoke(this, null);
        }
    }
}
