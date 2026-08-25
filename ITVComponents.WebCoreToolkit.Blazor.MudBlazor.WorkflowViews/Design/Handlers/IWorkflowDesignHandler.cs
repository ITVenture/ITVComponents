using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.ViewModels;
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.Model;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers
{
    /// <summary>
    /// Backend fuer die Design-Views: liest Workflow-Definitionen (fuer diese Phase nur lesend -
    /// Uebersicht und Graph-Ansicht). Der bearbeitende Editor kommt in einer spaeteren Phase.
    /// </summary>
    public interface IWorkflowDesignHandler
    {
        /// <summary>Prueft, ob der aktuelle Benutzer eine der Berechtigungen hat.</summary>
        bool HasPermission(params string[] permissions);

        /// <summary>
        /// Liefert eine Seite der Definition-Uebersicht (je Zeile eine Id+Version). <paramref name="environment"/>
        /// waehlt - falls konfiguriert - die Workflow-Umgebung (Store); null = die Standard-Umgebung.
        /// </summary>
        Task<PagedResult<WorkflowDefinitionListItem>> ListDefinitionsAsync(ClaimsPrincipal user, WorkflowListQuery query,
            string? environment = null);

        /// <summary>
        /// Laedt eine Definition. Ist <paramref name="version"/> null, wird die hoechste Version
        /// geliefert. Liefert null, wenn nichts gefunden wird. <paramref name="environment"/> waehlt die
        /// Umgebung (null = Standard).
        /// </summary>
        Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int? version,
            string? environment = null);

        /// <summary>
        /// Speichert eine Definition (Upsert nach Id+Version). Liefert false ohne
        /// <c>Workflow.Design</c>-Berechtigung, bei ungueltiger Eingabe oder wenn das Speichern
        /// fehlschlaegt (der Grund wird protokolliert). <paramref name="environment"/> waehlt die Umgebung
        /// (null = Standard).
        /// </summary>
        Task<bool> SaveDefinitionAsync(ClaimsPrincipal user, WorkflowDefinition definition,
            string? environment = null);

        /// <summary>
        /// Die verfuegbaren Aktivitaets-Typen aus dem Katalog der Instanz, deren <c>Name</c> dem
        /// <paramref name="executionTarget"/> entspricht (in der gewaehlten <paramref name="environment"/>).
        /// Ohne passendes Ziel/Instanz/Umgebung faellt es auf den per DI registrierten Default-Katalog zurueck
        /// (bisheriges Ein-Katalog-Verhalten).
        /// </summary>
        Task<IReadOnlyList<ActivityTypeInfo>> GetActivityTypesAsync(ClaimsPrincipal user, string? environment,
            string? executionTarget);

        /// <summary>Die deklarierten Parameter einer Aktivitaet aus dem Instanz-Katalog (Fallback wie oben).</summary>
        Task<IReadOnlyList<ActivityParameter>> GetActivityParametersAsync(ClaimsPrincipal user, string? environment,
            string? executionTarget, string activityRef);

        /// <summary>Die zulaessigen Werte eines Auswahl-Parameters aus dem Instanz-Katalog (Fallback wie oben).</summary>
        Task<IReadOnlyList<ActivityParameterValue>> GetActivityValidValuesAsync(ClaimsPrincipal user,
            string? environment, string? executionTarget, string activityRef, string parameterName);
    }
}
