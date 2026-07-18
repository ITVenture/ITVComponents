using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core.RuntimeSafety;
using ITVComponents.Scripting.CScript.Helpers;
using ITVComponents.Scripting.CScript.Security;

namespace ITVComponents.Scripting.CScript.Interpreter.Runtime
{
    /// <summary>
    /// Zustand einer einzelnen Script-Ausfuehrung.
    /// </summary>
    /// <remarks>
    /// Uebernimmt den Zustand, der beim ScriptVisitor auf der Visitor-Instanz lag und diese
    /// dadurch nicht wiederverwendbar machte (daher InterpreterBuffer/RunnerItem mit Pool und
    /// Lock). Weil der Knotenbaum jetzt unveraenderlich ist, braucht es diesen Pool nicht mehr:
    /// derselbe Baum kann von beliebig vielen Threads gleichzeitig ausgefuehrt werden, jeder
    /// mit eigenem Kontext.
    ///
    /// Die Flags loopJumpAllowed, catching und returnSupported des Visitors entfallen ersatzlos -
    /// diese Information steckt jetzt strukturell in <see cref="Completion"/> und im Frame-Stack.
    /// </remarks>
    public sealed class ExecutionContext
    {
        /// <summary>
        /// Initialisiert eine neue Ausfuehrung.
        /// </summary>
        /// <param name="variables">der Variablen-Scope der Ausfuehrung</param>
        /// <param name="policy">die geltende Sicherheits-Policy; null uebernimmt die des Scopes</param>
        /// <param name="scopeInitializer">Initialisierer fuer neu geoeffnete Funktions-Scopes</param>
        public ExecutionContext(IScope variables, ScriptingPolicy policy = null,
            InitializeScopeVariables scopeInitializer = null)
        {
            Variables = variables ?? throw new ArgumentNullException(nameof(variables));
            Policy = policy ?? ScriptingPolicy.Default;
            ScopeInitializer = scopeInitializer;
        }

        /// <summary>
        /// Der aktuelle Variablen-Scope. Bloecke oeffnen und schliessen darauf innere Scopes.
        /// </summary>
        public IScope Variables { get; }

        /// <summary>
        /// Die geltende Sicherheits-Policy.
        /// </summary>
        public ScriptingPolicy Policy { get; }

        /// <summary>
        /// Initialisierer, mit dem neu geoeffnete Scopes bestueckt werden.
        /// </summary>
        public InitializeScopeVariables ScopeInitializer { get; }

        /// <summary>
        /// Steuert, ob bei Zuweisungen und Operationen Typpruefungen greifen (Pragma @@TYPESAFETY).
        /// </summary>
        public bool TypeSafety { get; set; } = true;

        /// <summary>
        /// Steuert, ob Aufrufe ueber den Inline-Cache abgekuerzt werden duerfen (Pragma @@LAZYINVOKATION).
        /// </summary>
        public bool LazyInvokation { get; set; }

        /// <summary>
        /// Steuert, ob der Inline-Cache die Kompatibilitaetspruefung der Argumente ueberspringt
        /// (Pragma @@STATICBIND).
        /// </summary>
        public bool BypassCompatibilityOnLazyInvokation { get; set; }

        /// <summary>
        /// Der Debugger-Hook. Null bedeutet: keine Beobachtung, kein Overhead.
        /// </summary>
        public IExecutionObserver Observer { get; set; }

        /// <summary>
        /// Erzeugt eine Momentaufnahme des Zustands fuer den Debugger. Nur diese Aufnahme wird
        /// ueber die IPC-Schnittstelle transportiert - die Ausfuehrung selbst bleibt in-process.
        /// </summary>
        public ExecutionSnapshot CreateSnapshot()
        {
            return new ExecutionSnapshot(Variables.Snapshot());
        }
    }
}
