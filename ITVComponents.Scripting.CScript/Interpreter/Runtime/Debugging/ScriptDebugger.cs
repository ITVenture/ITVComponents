using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Ast;

namespace ITVComponents.Scripting.CScript.Runtime.Debugging
{
    /// <summary>
    /// Wird geworfen, wenn der Debugger die Ausfuehrung abbricht.
    /// </summary>
    public sealed class ScriptAbortedException : Exception
    {
        public ScriptAbortedException()
            : base("Die Ausfuehrung wurde vom Debugger abgebrochen.")
        {
        }
    }

    /// <summary>
    /// Haelt ein Script an Haltepunkten an und laesst es schrittweise weiterlaufen.
    /// </summary>
    /// <remarks>
    /// Der Debugger laeuft synchron im ausfuehrenden Thread: bei einem Halt wird der
    /// Rueckruf aufgerufen, und das Script laeuft erst weiter, wenn dieser zurueckkehrt. Damit
    /// bleibt die Ausfuehrung, wo sie ist - fuer entferntes Debuggen marshallt der Rueckruf den
    /// DebugStop ueber die IPC-Schnittstelle und wartet auf die Antwort der Oberflaeche.
    ///
    /// Kein Trampolin, kein ausgelagerter Ausfuehrungsstapel: fuer Anhalten und Weiterlaufen
    /// auf Anweisungsebene reicht es, den ausfuehrenden Thread im Rueckruf blockieren zu
    /// lassen. Einen expliziten Frame-Stack braeuchte es erst, um einen angehaltenen Lauf
    /// einzufrieren und anderswo fortzusetzen - und genau das ist ausdruecklich nicht das Ziel.
    ///
    /// Die aktuelle Sitzung haengt am Thread, damit aufgerufene Script-Funktionen sie finden:
    /// eine Funktion baut sich ihren eigenen ExecutionContext, in den der Debugger sonst nicht
    /// hineinreichte - man koennte dann nicht in Funktionen hineinsteppen.
    /// </remarks>
    public sealed class ScriptDebugger : IExecutionObserver
    {
        [ThreadStatic]
        private static ScriptDebugger current;

        private readonly Func<DebugStop, DebugCommand> onStop;
        private readonly HashSet<int> breakpoints = new HashSet<int>();
        private readonly List<Frame> frames = new List<Frame>();

        private DebugCommand mode = DebugCommand.Continue;

        /// <summary>
        /// Die Tiefe, auf die sich ein laufender Schritt bezieht.
        /// </summary>
        private int stepDepth;

        /// <param name="onStop">
        /// wird bei jedem Halt aufgerufen und entscheidet, wie es weitergeht. Blockiert dieser
        /// Rueckruf, steht das Script.
        /// </param>
        public ScriptDebugger(Func<DebugStop, DebugCommand> onStop)
        {
            this.onStop = onStop ?? throw new ArgumentNullException(nameof(onStop));
        }

        /// <summary>
        /// Die Sitzung, die im aktuellen Thread laeuft, oder null.
        /// </summary>
        internal static ScriptDebugger Current => current;

        /// <summary>
        /// Setzt einen Haltepunkt auf eine Zeile.
        /// </summary>
        public void AddBreakpoint(int line)
        {
            breakpoints.Add(line);
        }

        /// <summary>
        /// Entfernt einen Haltepunkt.
        /// </summary>
        public bool RemoveBreakpoint(int line)
        {
            return breakpoints.Remove(line);
        }

        /// <summary>
        /// Laesst das Script beim ersten ausgefuehrten Statement anhalten.
        /// </summary>
        public void BreakOnEntry()
        {
            mode = DebugCommand.StepInto;
        }

        /// <summary>
        /// Fuehrt etwas unter der Aufsicht dieses Debuggers aus.
        /// </summary>
        /// <remarks>
        /// Nur waehrend dieses Aufrufs finden aufgerufene Script-Funktionen die Sitzung.
        /// Verschachtelte Aufrufe stellen die vorherige Sitzung wieder her, damit ein Debugger
        /// einen anderen nicht verdraengt.
        /// </remarks>
        public T Run<T>(Func<T> action)
        {
            ScriptDebugger previous = current;
            current = this;
            try
            {
                return action();
            }
            finally
            {
                current = previous;
                frames.Clear();
            }
        }

        /// <summary>
        /// Meldet den Eintritt in eine Script-Funktion.
        /// </summary>
        internal IDisposable EnterFunction(string name, IScopeSnapshotSource scope)
        {
            frames.Insert(0, new Frame(name, scope));
            return new FrameHandle(this);
        }

        /// <inheritdoc/>
        public void BeforeStatement(IStatementNode node, ExecutionContext context)
        {
            EnsureRootFrame(context);

            string reason = Reason(node);
            if (reason == null)
            {
                return;
            }

            var stop = new DebugStop(node.Position, reason, BuildCallStack());
            Apply(onStop(stop));
        }

        /// <inheritdoc/>
        public void AfterStatement(IStatementNode node, ExecutionContext context, Completion completion)
        {
        }

        /// <summary>
        /// Entscheidet, ob an dieser Anweisung angehalten wird.
        /// </summary>
        private string Reason(IStatementNode node)
        {
            if (node.Position.IsKnown && breakpoints.Contains(node.Position.Line))
            {
                return "Breakpoint";
            }

            switch (mode)
            {
                case DebugCommand.StepInto:
                    return "Step";
                case DebugCommand.StepOver:
                    // Auf gleicher oder flacherer Ebene halten - ein Aufruf wird uebersprungen.
                    return frames.Count <= stepDepth ? "Step" : null;
                case DebugCommand.StepOut:
                    return frames.Count < stepDepth ? "Step" : null;
                default:
                    return null;
            }
        }

        private void Apply(DebugCommand command)
        {
            if (command == DebugCommand.Stop)
            {
                throw new ScriptAbortedException();
            }

            mode = command;
            stepDepth = frames.Count;
        }

        /// <summary>
        /// Sorgt dafuer, dass der aeusserste Rahmen existiert - er traegt die Variablen der
        /// obersten Ebene.
        /// </summary>
        private void EnsureRootFrame(ExecutionContext context)
        {
            if (frames.Count == 0)
            {
                frames.Add(new Frame(null, new ScopeSource(context)));
            }
        }

        private IReadOnlyList<CallStackFrame> BuildCallStack()
        {
            var retVal = new List<CallStackFrame>(frames.Count);
            foreach (Frame frame in frames)
            {
                retVal.Add(new CallStackFrame(frame.Name, frame.Scope.Snapshot()));
            }

            return retVal;
        }

        private void LeaveFunction()
        {
            if (frames.Count != 0)
            {
                frames.RemoveAt(0);
            }
        }

        private sealed class Frame
        {
            public Frame(string name, IScopeSnapshotSource scope)
            {
                Name = name;
                Scope = scope;
            }

            public string Name { get; }

            public IScopeSnapshotSource Scope { get; }
        }

        private sealed class ScopeSource : IScopeSnapshotSource
        {
            private readonly ExecutionContext context;

            public ScopeSource(ExecutionContext context)
            {
                this.context = context;
            }

            public Dictionary<string, object> Snapshot()
            {
                return context.Variables.Snapshot();
            }
        }

        private sealed class FrameHandle : IDisposable
        {
            private readonly ScriptDebugger owner;

            public FrameHandle(ScriptDebugger owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                owner.LeaveFunction();
            }
        }
    }

    /// <summary>
    /// Liefert eine Momentaufnahme eines Variablenbereichs.
    /// </summary>
    public interface IScopeSnapshotSource
    {
        Dictionary<string, object> Snapshot();
    }
}
