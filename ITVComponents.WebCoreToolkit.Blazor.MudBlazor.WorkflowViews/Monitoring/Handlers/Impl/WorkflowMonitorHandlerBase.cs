using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Stores;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl
{
    /// <summary>
    /// Gemeinsame Basis der Monitoring-Handler: listet ueber den EF-Kontext (indizierte Spalten), laedt
    /// Detail-Instanzen ueber den Store und bricht ueber die Engine ab. Der EINZIGE Unterschied zwischen
    /// den Varianten ist die <b>Signal-Zustellung</b> (<see cref="DeliverSignalAsync"/>) - deshalb ist sie
    /// hier als Naht ausgelagert:
    /// <list type="bullet">
    ///   <item><see cref="WorkflowMonitorHandler"/> - inline (advanced im Web-Prozess; Web-Only).</item>
    ///   <item><see cref="SplitWorkflowMonitorHandler"/> - store-only reaktivieren; die Ausfuehrung
    ///   uebernimmt ein (Backend-)Runner (getrennte Deployments).</item>
    /// </list>
    /// </summary>
    internal abstract class WorkflowMonitorHandlerBase : IWorkflowMonitorHandler
    {
        private readonly IServiceProvider services;
        private readonly IDbContextFactory<WorkflowContext> dbFactory;
        private readonly IWorkflowStore store;

        protected WorkflowMonitorHandlerBase(IServiceProvider services,
            IDbContextFactory<WorkflowContext> dbFactory, IWorkflowStore store, WorkflowEngine engine)
        {
            this.services = services;
            this.dbFactory = dbFactory;
            this.store = store;
            Engine = engine;
        }

        /// <summary>Die Engine (fuer Signal-Zustellung und Abbruch).</summary>
        protected WorkflowEngine Engine { get; }

        /// <inheritdoc/>
        public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        {
            return services.VerifyUserPermissions(permissions);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<WorkflowInstanceListItem>> ListInstancesAsync(ClaimsPrincipal user, ListQuery query)
        {
            await using WorkflowContext ctx = await dbFactory.CreateDbContextAsync();
            IQueryable<WorkflowInstanceRow> q = ctx.WorkflowInstances.AsNoTracking();

            if (query.Status.HasValue)
            {
                int status = query.Status.Value;
                q = q.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string term = query.Search!;
                q = q.Where(r => r.Id.Contains(term)
                                 || r.DefinitionId.Contains(term)
                                 || (r.CorrelationKey != null && r.CorrelationKey.Contains(term)));
            }

            int total = await q.CountAsync();
            q = Sort(q, query.SortColumn, query.SortDescending)
                .Skip(query.Page * query.PageSize)
                .Take(query.PageSize);

            var items = (await q.ToListAsync()).Select(ToListItem).ToList();
            return new PagedResult<WorkflowInstanceListItem> { Items = items, TotalCount = total };
        }

        /// <inheritdoc/>
        public Task<WorkflowInstance?> GetInstanceAsync(ClaimsPrincipal user, string instanceId)
        {
            return Task.FromResult<WorkflowInstance?>(store.GetInstance(instanceId));
        }

        /// <inheritdoc/>
        public Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int version)
        {
            return Task.FromResult<WorkflowDefinition?>(store.GetDefinition(definitionId, version));
        }

        /// <inheritdoc/>
        public Task<bool> SignalAsync(ClaimsPrincipal user, string instanceId, string signalName)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate })
                || store.GetInstance(instanceId) == null)
            {
                return Task.FromResult(false);
            }

            return DeliverSignalAsync(instanceId, signalName);
        }

        /// <inheritdoc/>
        public Task<bool> CancelAsync(ClaimsPrincipal user, string instanceId)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return Task.FromResult(false);
            }

            // Abbruch ist in JEDEM Deployment eine reine Store-Operation (keine Aktivitaet laeuft) - daher
            // hier gemeinsam, unabhaengig von der Signal-Variante.
            return Task.FromResult(Engine.CancelWorkflow(instanceId));
        }

        /// <summary>
        /// Stellt ein Signal an die (existierende, berechtigte) Instanz zu. Die Variante bestimmt, OB dabei
        /// im Web-Prozess advanced wird (inline) oder nur store-only reaktiviert wird (Runner treibt voran).
        /// </summary>
        protected abstract Task<bool> DeliverSignalAsync(string instanceId, string signalName);

        private static WorkflowInstanceListItem ToListItem(WorkflowInstanceRow row)
        {
            return new WorkflowInstanceListItem
            {
                Id = row.Id,
                DefinitionId = row.DefinitionId,
                DefinitionVersion = row.DefinitionVersion,
                Status = ((WorkflowStatus)row.Status).ToString(),
                CorrelationKey = row.CorrelationKey,
                CreatedUtc = row.CreatedUtc,
                UpdatedUtc = row.UpdatedUtc
            };
        }

        private static IQueryable<WorkflowInstanceRow> Sort(IQueryable<WorkflowInstanceRow> q, string? column,
            bool descending)
        {
            switch (column)
            {
                case "DefinitionId":
                    return descending ? q.OrderByDescending(r => r.DefinitionId) : q.OrderBy(r => r.DefinitionId);
                case "Status":
                    return descending ? q.OrderByDescending(r => r.Status) : q.OrderBy(r => r.Status);
                case "CorrelationKey":
                    return descending ? q.OrderByDescending(r => r.CorrelationKey) : q.OrderBy(r => r.CorrelationKey);
                case "CreatedUtc":
                    return descending ? q.OrderByDescending(r => r.CreatedUtc) : q.OrderBy(r => r.CreatedUtc);
                default:
                    // Standard: zuletzt geaenderte zuerst.
                    return q.OrderByDescending(r => r.UpdatedUtc);
            }
        }
    }
}
