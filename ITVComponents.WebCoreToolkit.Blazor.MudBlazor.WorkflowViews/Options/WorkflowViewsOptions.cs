namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options
{
    /// <summary>
    /// Wie ein Operator-Signal aus dem Monitoring zugestellt wird.
    /// </summary>
    public enum WorkflowSignalDelivery
    {
        /// <summary>
        /// Inline: die Engine advanced die Instanz synchron IM WEB-PROZESS bis zum naechsten Wartepunkt
        /// (fuehrt die folgenden Aktivitaeten hier aus). Passend fuer Web-Only, wo Editor und Engine im
        /// selben Prozess laufen.
        /// </summary>
        Inline,

        /// <summary>
        /// Runner: das Signal wird nur store-only zugestellt (wartende Tokens werden reaktiviert, ohne im
        /// Web zu advancen); die Ausfuehrung uebernimmt ein (Backend-)<c>WorkflowRunner</c> auf derselben
        /// Datenbank. Passend fuer getrennte Deployments, in denen die Aktivitaeten NICHT im Web laufen.
        /// </summary>
        Runner
    }

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

        /// <summary>
        /// Die Obergrenze fuer einen <b>Anhang</b> in Bytes. Standard 10 MB; 0 oder kleiner schaltet
        /// Anhaenge ganz ab (dann erscheint auch kein Knopf dafuer).
        /// </summary>
        /// <remarks>
        /// Die Grenze steht hier und nicht in der Ablage: sie ist eine Aussage darueber, was in DIESER
        /// Anlage zumutbar ist, und nicht darueber, was die Ablage technisch koennte. Die eingebaute
        /// Ablage legt die Bytes in der Datenbank ab - wer regelmaessig grosse Dateien erwartet, hebt
        /// nicht diese Zahl an, sondern registriert eine eigene <c>IWorkflowAttachmentStore</c>-Umsetzung.
        /// </remarks>
        public long MaxAttachmentBytes { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Wie ein Operator-Signal zugestellt wird. Standard <see cref="WorkflowSignalDelivery.Inline"/>
        /// (Web-Only). Fuer getrennte Deployments (Engine/Runner im Backend) auf
        /// <see cref="WorkflowSignalDelivery.Runner"/> stellen - dann reaktiviert das Web nur store-only,
        /// und der Backend-Runner treibt voran (die Aktivitaeten laufen NICHT im Web).
        /// </summary>
        public WorkflowSignalDelivery SignalDelivery { get; set; } = WorkflowSignalDelivery.Inline;
    }
}
