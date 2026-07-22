using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Ast;

namespace ITVComponents.Scripting.CScript.Runtime.Debugging
{
    /// <summary>
    /// Was der Debugger nach einem Halt als Naechstes tun soll.
    /// </summary>
    public enum DebugCommand
    {
        /// <summary>Weiterlaufen bis zum naechsten Haltepunkt.</summary>
        Continue,

        /// <summary>Eine Anweisung weiter, auch in aufgerufene Funktionen hinein.</summary>
        StepInto,

        /// <summary>Eine Anweisung weiter, ueber Funktionsaufrufe hinweg.</summary>
        StepOver,

        /// <summary>Bis zum Verlassen der aktuellen Funktion weiterlaufen.</summary>
        StepOut,

        /// <summary>Die Ausfuehrung abbrechen.</summary>
        Stop
    }

    /// <summary>
    /// Ein Eintrag des Aufruf-Stapels.
    /// </summary>
    /// <remarks>
    /// Reine Daten, damit der Eintrag ueber die IPC-Schnittstelle an eine Oberflaeche gereicht
    /// werden kann.
    /// </remarks>
    public sealed class CallStackFrame
    {
        public CallStackFrame(string functionName, IReadOnlyDictionary<string, object> variables)
        {
            FunctionName = functionName;
            Variables = variables;
        }

        /// <summary>Der Name der Funktion, oder null bei der aeussersten Ebene.</summary>
        public string FunctionName { get; }

        /// <summary>Die in diesem Rahmen sichtbaren Variablen.</summary>
        public IReadOnlyDictionary<string, object> Variables { get; }
    }

    /// <summary>
    /// Der Zustand des angehaltenen Scripts.
    /// </summary>
    /// <remarks>
    /// Was ein Debugger anzeigt. Bewusst eine Momentaufnahme aus reinen Daten und vom laufenden
    /// Zustand entkoppelt: fuer entferntes Debuggen ueber die InterProcessCommunication-
    /// Schnittstelle wandert diese Aufnahme zur Oberflaeche, waehrend das Script in seinem
    /// Dienst weiterlebt. Zustands-Inspektion, nicht Zustands-Verlagerung.
    /// </remarks>
    public sealed class DebugStop
    {
        internal DebugStop(SourcePosition position, string reason, IReadOnlyList<CallStackFrame> callStack)
        {
            Position = position;
            Reason = reason;
            CallStack = callStack;
        }

        /// <summary>Wo im Quelltext das Script steht.</summary>
        public SourcePosition Position { get; }

        /// <summary>Warum angehalten wurde - ein Haltepunkt oder ein Einzelschritt.</summary>
        public string Reason { get; }

        /// <summary>
        /// Der Aufruf-Stapel, innerste Ebene zuerst. Der erste Eintrag traegt die Variablen,
        /// die an der aktuellen Stelle sichtbar sind.
        /// </summary>
        public IReadOnlyList<CallStackFrame> CallStack { get; }

        /// <summary>
        /// Die an der aktuellen Stelle sichtbaren Variablen.
        /// </summary>
        public IReadOnlyDictionary<string, object> Variables =>
            CallStack.Count != 0 ? CallStack[0].Variables : new Dictionary<string, object>();
    }
}
