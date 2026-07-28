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

        /// <summary>
        /// Aspekt: die eigenen Aufgaben sehen und erledigen. Bewusst getrennt von
        /// <see cref="Monitor"/>/<see cref="Operate"/>: die Arbeitsliste ist eine <b>Benutzer</b>-Ansicht -
        /// wer Rechnungen freigibt, braucht deswegen keinen Blick in fremde Instanzen. Die feine
        /// Zustaendigkeit macht die Permission am Knoten
        /// (<see cref="ITVComponents.Workflow.Model.UserActivityNode.RequiredPermission"/>).
        /// </summary>
        public const string Tasks = "Workflow.Tasks";
    }
}
