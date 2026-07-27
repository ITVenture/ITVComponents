namespace ITVComponents.Workflow.Runtime
{
    /// <summary>
    /// Ein Dienst, dem die Engine ihre geteilte <see cref="WorkflowRuntimeContext"/> nach dem
    /// Zusammenbau "reindrueckt".
    /// </summary>
    /// <remarks>
    /// Setter-only: der Verantwortliche (das Engine-Umfeld bzw. die PluginFactory ueber ihr
    /// PluginInitialized-Ereignis) setzt den Kontext garantiert, bevor der Dienst genutzt wird. So
    /// bleibt der Kern frei von einer Abhaengigkeit auf ITVComponents.Plugins, und geteilte Laufzeit-
    /// Objekte muessen nicht durch Konstruktor-Strings gefaedelt werden.
    /// </remarks>
    public interface IWorkflowRuntimeAware
    {
        /// <summary>Wird vom Besitzer der Laufzeit-Umgebung gesetzt.</summary>
        WorkflowRuntimeContext Runtime { set; }
    }
}
