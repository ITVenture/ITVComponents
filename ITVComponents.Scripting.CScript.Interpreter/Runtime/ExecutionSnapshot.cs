using System.Collections.Generic;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Momentaufnahme eines angehaltenen Scripts: das, was ein Debugger anzeigt.
    /// </summary>
    /// <remarks>
    /// Bewusst reine Daten und vom Live-Zustand entkoppelt, damit die Aufnahme ueber die
    /// InterProcessCommunication-Schnittstelle an eine Web-Oberflaeche gereicht werden kann,
    /// waehrend das Script in seinem Backend-Service weiterlebt. Zustands-Inspektion,
    /// nicht Zustands-Migration.
    /// </remarks>
    public sealed class ExecutionSnapshot
    {
        internal ExecutionSnapshot(Dictionary<string, object> variables)
        {
            Variables = variables;
        }

        /// <summary>
        /// Alle im aktuellen Scope sichtbaren Variablen.
        /// </summary>
        public IReadOnlyDictionary<string, object> Variables { get; }
    }
}
