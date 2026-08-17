using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.EntityFramework.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.Workflow.EntityFramework
{
    /// <summary>
    /// Die Themen, unter denen Aenderungen am Workflow-Kontext gemeldet werden.
    /// </summary>
    public static class WorkflowChangeTopics
    {
        /// <summary>
        /// Der Fortschritt einer Instanz: Tokens und Instanz-Zeilen. Wer darauf hoert, erfaehrt, dass sich
        /// <b>irgendwo</b> etwas bewegt hat - nicht, wo.
        /// </summary>
        /// <remarks>
        /// Ein Thema ist eine Menge von TABELLEN, keine Zeilenmenge: die Instanz-Id steht nicht in der
        /// Meldung. Fuer den Verbraucher heisst das, dass die Meldung ein <b>Weckruf</b> ist und die
        /// Wahrheit weiterhin in der Datenbank steht - er fragt nach dem Aufwachen selbst nach, was seine
        /// Instanz betrifft. Das genuegt fuer einen wartenden Dialog; als allgemeiner Ereignis-Verteiler
        /// pro Instanz waere es das falsche Werkzeug.
        /// </remarks>
        public const string Progress = "WorkflowProgress";
    }

    /// <summary>
    /// Verdrahtet das Aenderungs-Signal fuer den Workflow-Kontext.
    /// </summary>
    public static class WorkflowChangeSignalExtensions
    {
        /// <summary>
        /// Registers the change-signal for the workflow context and maps its tokens and instances onto
        /// <see cref="WorkflowChangeTopics.Progress"/>.
        /// </summary>
        /// <param name="services">the services-collection to register in</param>
        /// <returns>the same services-collection</returns>
        /// <remarks>
        /// <para>
        /// Damit hier etwas ankommt, braucht der Workflow-Kontext den Schreib-Verfolger
        /// (<c>EntityWriteTrackerInterceptorOptionsLoader</c> in seiner Plugin-Konfiguration) <b>und</b>
        /// einen registrierten <c>IEntityWriteTracker&lt;WorkflowContext&gt;</c> - im Toolkit-Host erledigt
        /// das <c>ActivationSettings.UseEntityTracker</c>, das die Registrierung offen generisch vornimmt.
        /// Fehlt eines von beidem, bleibt das Signal ein stiller No-op; die Verbraucher fallen dann auf ihr
        /// eigenes Nachfragen zurueck.
        /// </para>
        /// <para>
        /// Bewusst OHNE den nicht-generischen Alias (siehe <c>AddEntityChangeSignal</c> ohne Typ-Argument):
        /// der gehoert dem Sicherheits-Kontext.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddWorkflowChangeSignal(this IServiceCollection services)
        {
            services.AddEntityChangeSignal();
            services.Configure<EntitySignalOptions<WorkflowContext>>(o =>
                o.Add(WorkflowChangeTopics.Progress, typeof(TokenRow), typeof(WorkflowInstanceRow)));
            return services;
        }
    }
}
