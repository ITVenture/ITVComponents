using ITVComponents.Workflow.Instances;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring
{
    /// <summary>
    /// Uebersetzt die <b>Wertelisten</b> der Betriebs-Oberflaeche - Status und Prioritaet. Sie sind
    /// Aufzaehlungen des Kerns und tragen dort englische Namen; angezeigt werden sie in der Sprache des
    /// Benutzers.
    /// </summary>
    /// <remarks>
    /// Eine eigene Stelle, weil dieselben Werte in mehreren Ansichten auftauchen (Liste, Detail,
    /// Start-Dialog) und eine Tabelle mit deutscher Ueberschrift und englischen Werten schlechter zu
    /// lesen ist als eine durchgehend englische.
    /// <para>
    /// Faellt der Schluessel aus, wird der <b>Rohwert</b> gezeigt, nicht der Schluesselname: ein neu
    /// hinzugekommener Status soll als "Escalated" erscheinen und nicht als "StatusEscalated".
    /// </para>
    /// </remarks>
    public static class WorkflowMonitorText
    {
        /// <summary>Der Status einer Instanz, uebersetzt.</summary>
        public static string Status(IStringLocalizer<WorkflowMonitorMessages> localizer, string status)
        {
            if (string.IsNullOrEmpty(status))
            {
                return "—";
            }

            LocalizedString text = localizer[$"Status{status}"];
            return text.ResourceNotFound ? status : text.Value;
        }

        /// <summary>Der Status einer Instanz, uebersetzt.</summary>
        public static string Status(IStringLocalizer<WorkflowMonitorMessages> localizer, WorkflowStatus status)
            => Status(localizer, status.ToString());

        /// <summary>
        /// Der Name einer Prioritaets-Stufe, uebersetzt. Die Zahl steht bewusst NICHT mit dabei - wo sie
        /// gebraucht wird (Auswahlliste), haengt der Aufrufer sie an.
        /// </summary>
        public static string Priority(IStringLocalizer<WorkflowMonitorMessages> localizer, int priority)
        {
            string name = WorkflowPriority.Name(priority);
            LocalizedString text = localizer[$"Priority{name}"];
            return text.ResourceNotFound ? name : text.Value;
        }
    }
}
