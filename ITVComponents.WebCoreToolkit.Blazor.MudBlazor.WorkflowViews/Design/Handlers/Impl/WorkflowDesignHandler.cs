using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Model;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers.Impl
{
    /// <summary>
    /// Standard-Implementierung von <see cref="IWorkflowDesignHandler"/>. Jede Operation laeuft ueber
    /// eine <see cref="WorkflowOperation"/>, die je Zugriff einen FRISCHEN <c>WorkflowContext</c> zieht
    /// (DI/global oder Plugin/per-Tenant) - Blazor-/tenant-sicher, ohne circuit-lang geteilten Store.
    /// Listet ueber den EF-Kontext (Schluesselspalten Id+Version) und laedt/speichert einzelne
    /// Definitionen ueber den Store. Eine Engine wird hier nicht gebraucht.
    /// </summary>
    internal sealed class WorkflowDesignHandler : IWorkflowDesignHandler
    {
        private readonly IServiceProvider services;
        private readonly IFreshInjectablePlugin<WorkflowContext> freshContext;

        public WorkflowDesignHandler(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
        {
            this.services = services;
            this.freshContext = freshContext;
        }

        // Design-Operationen brauchen keine Engine -> keine Engine-Factory. Der Store richtet sich nach der
        // (optional) gewaehlten Umgebung: deren WorkflowStorePluginName benennt die zu leasende
        // WorkflowContext-Dependency; ohne Umgebung/Settings bleibt es der Standard-Store.
        private WorkflowOperation BeginOperation(string? environment)
            => new WorkflowOperation(freshContext,
                storeDependencyName: WorkflowEnvironmentResolver.StoreDependencyName(services, environment));

        /// <inheritdoc/>
        public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        {
            return services.VerifyUserPermissions(permissions);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<WorkflowDefinitionListItem>> ListDefinitionsAsync(ClaimsPrincipal user, ListQuery query,
            string? environment = null)
        {
            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            IQueryable<WorkflowDefinitionRow> q = ctx.WorkflowDefinitions.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string term = query.Search!;
                q = q.Where(r => r.Id.Contains(term));
            }

            int total = await q.CountAsync();
            q = Sort(q, query.SortColumn, query.SortDescending)
                .Skip(query.Page * query.PageSize)
                .Take(query.PageSize);

            var items = (await q.ToListAsync()).Select(ToListItem).ToList();
            return new PagedResult<WorkflowDefinitionListItem> { Items = items, TotalCount = total };
        }

        /// <inheritdoc/>
        public Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int? version,
            string? environment = null)
        {
            using WorkflowOperation op = BeginOperation(environment);
            return Task.FromResult<WorkflowDefinition?>(op.Store.GetDefinition(definitionId, version));
        }

        /// <inheritdoc/>
        public Task<bool> SaveDefinitionAsync(ClaimsPrincipal user, WorkflowDefinition definition,
            string? environment = null)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Design }))
            {
                LogEnvironment.LogEvent(
                    "Speichern einer Workflow-Definition ohne Berechtigung 'Workflow.Design' abgelehnt.",
                    LogSeverity.Warning);
                return Task.FromResult(false);
            }

            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                LogEnvironment.LogEvent(
                    "Workflow-Definition nicht gespeichert: keine oder leere Id.", LogSeverity.Error);
                return Task.FromResult(false);
            }

            try
            {
                using WorkflowOperation op = BeginOperation(environment);
                op.Store.SaveDefinition(definition);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Konnte Workflow-Definition '{definition.Id}' v{definition.Version} nicht speichern: {ex.OutlineException()}",
                    LogSeverity.Error);
                return Task.FromResult(false);
            }
        }

        private static WorkflowDefinitionListItem ToListItem(WorkflowDefinitionRow row)
        {
            // Die Zaehl-/Namensfelder stecken nur im JSON-Blob. Fuer die Uebersicht reicht ein
            // schemaloses Lesen (Nodes/Flows haben Diskriminator-Attribute); Objektwerte in der
            // Konfiguration werden hier nicht gelesen, daher genuegen die Standardoptionen.
            string? name = null;
            int nodeCount = 0;
            int flowCount = 0;
            try
            {
                WorkflowDefinition? def = JsonSerializer.Deserialize<WorkflowDefinition>(row.DefinitionJson);
                if (def != null)
                {
                    name = def.Name;
                    nodeCount = def.Nodes?.Count ?? 0;
                    flowCount = def.Flows?.Count ?? 0;
                }
            }
            catch (Exception ex)
            {
                // Bricht die Uebersicht nicht ab - die Zeile erscheint dann ohne Metadaten. Der
                // Grund (kaputtes/aelteres JSON) muss aber nachvollziehbar im Log stehen.
                LogEnvironment.LogEvent(
                    $"Konnte Metadaten der Definition '{row.Id}' v{row.Version} nicht lesen: {ex.OutlineException()}",
                    LogSeverity.Error);
            }

            return new WorkflowDefinitionListItem
            {
                Id = row.Id,
                Version = row.Version,
                Name = name,
                NodeCount = nodeCount,
                FlowCount = flowCount
            };
        }

        private static IQueryable<WorkflowDefinitionRow> Sort(IQueryable<WorkflowDefinitionRow> q, string? column,
            bool descending)
        {
            switch (column)
            {
                case "Version":
                    return descending
                        ? q.OrderByDescending(r => r.Version)
                        : q.OrderBy(r => r.Version);
                default:
                    // Standard: nach Id, neueste Version zuerst.
                    return descending
                        ? q.OrderByDescending(r => r.Id).ThenByDescending(r => r.Version)
                        : q.OrderBy(r => r.Id).ThenByDescending(r => r.Version);
            }
        }
    }
}
