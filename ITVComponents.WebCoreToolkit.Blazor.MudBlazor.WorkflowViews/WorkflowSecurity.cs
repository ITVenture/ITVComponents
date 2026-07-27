namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews
{
    /// <summary>
    /// Die Namen von Feature und Berechtigungen dieses Moduls. Das Feature schaltet den Bereich
    /// frei, die Berechtigungen gaten die einzelnen Aspekte.
    /// </summary>
    public static class WorkflowSecurity
    {
        /// <summary>Feature, das den gesamten Workflow-Bereich freischaltet (pro Mandant aktiviert).</summary>
        public const string Feature = "ITVWorkflow";

        /// <summary>Aspekt: laufende Instanzen ansehen (read-only).</summary>
        public const string Monitor = "Workflow.Monitor";

        /// <summary>Aspekt: operativ eingreifen (Signal senden, abbrechen).</summary>
        public const string Operate = "Workflow.Operate";

        /// <summary>Aspekt: Definitionen anlegen/bearbeiten (Editor).</summary>
        public const string Design = "Workflow.Design";
    }
}
