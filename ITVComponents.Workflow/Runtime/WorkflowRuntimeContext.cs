namespace ITVComponents.Workflow.Runtime
{
    /// <summary>
    /// Die geteilte Laufzeit-Umgebung einer Engine, die an mitwirkende Dienste weitergereicht wird.
    /// </summary>
    /// <remarks>
    /// Bewusst NICHT ueber Konstruktor-Argumente verteilt: ein zur Kompositionszeit erzeugtes,
    /// geteiltes Objekt (wie das <see cref="InstanceGate"/>) laesst sich nicht aus einem Plugin-
    /// Konstruktions-String aufloesen. Stattdessen wird der Kontext nach dem Zusammenbau als
    /// garantiert gesetztes Property ueber <see cref="IWorkflowRuntimeAware"/> "reingedrueckt".
    /// Bewusst erweiterbar - hier kommen spaeter z.B. eine Engine-Referenz oder ein Abbruch-Token dazu.
    /// </remarks>
    public sealed class WorkflowRuntimeContext
    {
        /// <summary>Die Pro-Instanz-Sperre, die den gleichzeitigen Vortrieb derselben Instanz verhindert.</summary>
        public InstanceGate Gate { get; init; } = new InstanceGate();
    }
}
