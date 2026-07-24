namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options
{
    /// <summary>
    /// Konfiguration des Workflow-View-Moduls (Host-`appsettings`, Sektion des WebParts).
    /// </summary>
    public sealed class WorkflowViewsOptions
    {
        /// <summary>
        /// Schaltet die Registrierung der Views/Handler ein. Erwartet, dass der Host
        /// <c>WorkflowContext</c> (als <c>IDbContextFactory</c>), einen <c>IWorkflowStore</c> und
        /// eine <c>WorkflowEngine</c> registriert hat.
        /// </summary>
        public bool ConfigureViews { get; set; }
    }
}
