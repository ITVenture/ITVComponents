using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.ViewModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Design.Handlers.Impl
{
    /// <summary>
    /// Standard-Implementierung von <see cref="IWorkflowDesignHandler"/>. Listet ueber den
    /// EF-Kontext (Schluesselspalten Id+Version) und laedt einzelne Definitionen ueber den Store.
    /// </summary>
    internal sealed class WorkflowDesignHandler : IWorkflowDesignHandler
    {
        private readonly IServiceProvider services;
        private readonly IDbContextFactory<WorkflowContext> dbFactory;
        private readonly IWorkflowStore store;

        public WorkflowDesignHandler(IServiceProvider services, IDbContextFactory<WorkflowContext> dbFactory,
            IWorkflowStore store)
        {
            this.services = services;
            this.dbFactory = dbFactory;
            this.store = store;
        }

        /// <inheritdoc/>
        public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        {
            return services.VerifyUserPermissions(permissions);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<WorkflowDefinitionListItem>> ListDefinitionsAsync(ClaimsPrincipal user, ListQuery query)
        {
            await using WorkflowContext ctx = await dbFactory.CreateDbContextAsync();
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
        public Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int? version)
        {
            return Task.FromResult<WorkflowDefinition?>(store.GetDefinition(definitionId, version));
        }

        /// <inheritdoc/>
        public Task<bool> SaveDefinitionAsync(ClaimsPrincipal user, WorkflowDefinition definition)
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
                store.SaveDefinition(definition);
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
