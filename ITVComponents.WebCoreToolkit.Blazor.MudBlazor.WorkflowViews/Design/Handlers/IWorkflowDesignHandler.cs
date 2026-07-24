using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.ViewModels;
using ITVComponents.Workflow.Model;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers
{
    /// <summary>
    /// Backend fuer die Design-Views: liest Workflow-Definitionen (fuer diese Phase nur lesend -
    /// Uebersicht und Graph-Ansicht). Der bearbeitende Editor kommt in einer spaeteren Phase.
    /// </summary>
    public interface IWorkflowDesignHandler
    {
        /// <summary>Prueft, ob der aktuelle Benutzer eine der Berechtigungen hat.</summary>
        bool HasPermission(ClaimsPrincipal user, params string[] permissions);

        /// <summary>Liefert eine Seite der Definition-Uebersicht (je Zeile eine Id+Version).</summary>
        Task<PagedResult<WorkflowDefinitionListItem>> ListDefinitionsAsync(ClaimsPrincipal user, ListQuery query);

        /// <summary>
        /// Laedt eine Definition. Ist <paramref name="version"/> null, wird die hoechste Version
        /// geliefert. Liefert null, wenn nichts gefunden wird.
        /// </summary>
        Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int? version);

        /// <summary>
        /// Speichert eine Definition (Upsert nach Id+Version). Liefert false ohne
        /// <c>Workflow.Design</c>-Berechtigung, bei ungueltiger Eingabe oder wenn das Speichern
        /// fehlschlaegt (der Grund wird protokolliert).
        /// </summary>
        Task<bool> SaveDefinitionAsync(ClaimsPrincipal user, WorkflowDefinition definition);
    }
}
