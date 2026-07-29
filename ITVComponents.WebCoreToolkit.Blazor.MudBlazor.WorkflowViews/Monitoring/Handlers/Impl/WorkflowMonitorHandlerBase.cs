using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.WebWorker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.Handlers.Impl
{
    /// <summary>
    /// Gemeinsame Basis der Monitoring-Handler. Jede Operation laeuft ueber eine
    /// <see cref="WorkflowOperation"/>, die je Store-/Abfrage-Zugriff einen FRISCHEN
    /// <c>WorkflowContext</c> zieht (DI/global oder Plugin/per-Tenant) - so ist der Handler
    /// Blazor-/tenant-sicher, statt einen circuit-lang geteilten Store/Engine/DbContext zu halten.
    /// Der EINZIGE Unterschied zwischen den Varianten ist die <b>Signal-Zustellung</b>
    /// (<see cref="DeliverSignalAsync"/>):
    /// <list type="bullet">
    ///   <item><see cref="WorkflowMonitorHandler"/> - inline (advanced im Web-Prozess; setzt den
    ///   globalen Ein-Kontext-Betrieb voraus).</item>
    ///   <item><see cref="SplitWorkflowMonitorHandler"/> - store-only reaktivieren; die Ausfuehrung
    ///   uebernimmt ein (tenant-uebergreifender) Runner (getrennte Deployments / Multi-Tenant).</item>
    /// </list>
    /// </summary>
    internal abstract class WorkflowMonitorHandlerBase : IWorkflowMonitorHandler
    {
        private readonly IServiceProvider services;
        private readonly IFreshInjectablePlugin<WorkflowContext> freshContext;

        protected WorkflowMonitorHandlerBase(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
        {
            this.services = services;
            this.freshContext = freshContext;
        }

        /// <summary>
        /// Oeffnet eine neue Operation. Die Engine-Factory wird optional aufgeloest - fehlt sie, wirft
        /// erst ein tatsaechlicher Engine-Zugriff (Signal/Abbruch) mit erklaerender Meldung.
        /// </summary>
        protected WorkflowOperation BeginOperation(string? environment = null)
            => new WorkflowOperation(freshContext, services.GetService<WorkflowEngineFactory>(),
                WorkflowEnvironmentResolver.StoreDependencyName(services, environment));

        /// <inheritdoc/>
        public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        {
            return services.VerifyUserPermissions(permissions);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<WorkflowInstanceListItem>> ListInstancesAsync(ClaimsPrincipal user, ListQuery query,
            string? environment = null)
        {
            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
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
        public Task<WorkflowInstance?> GetInstanceAsync(ClaimsPrincipal user, string instanceId, string? environment = null)
        {
            using WorkflowOperation op = BeginOperation(environment);
            return Task.FromResult<WorkflowInstance?>(op.Store.GetInstance(instanceId));
        }

        /// <inheritdoc/>
        public Task<WorkflowDefinition?> GetDefinitionAsync(ClaimsPrincipal user, string definitionId, int version,
            string? environment = null)
        {
            using WorkflowOperation op = BeginOperation(environment);
            return Task.FromResult<WorkflowDefinition?>(op.Store.GetDefinition(definitionId, version));
        }

        /// <inheritdoc/>
        public async Task<bool> SignalAsync(ClaimsPrincipal user, string instanceId, string signalName,
            string? environment = null)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return false;
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowInstance? instance = op.Store.GetInstance(instanceId);
            if (instance == null)
            {
                return false;
            }

            bool delivered = await DeliverSignalAsync(op, instanceId, signalName);
            if (delivered)
            {
                // Best-effort Wake: einen (evtl. im selben Prozess laufenden) Worker sofort auf diesen Tenant/
                // diese Umgebung aufmerksam machen, statt ihn bis zum Max-Linger warten zu lassen. Fehlt der
                // Worker (kein Split-/Worker-Betrieb), ist der Service nicht registriert -> stiller No-op.
                services.GetService<IWorkflowWorkerWake>()?.Poke(environment, instance.TenantId);
            }

            return delivered;
        }

        /// <inheritdoc/>
        public Task<bool> CancelAsync(ClaimsPrincipal user, string instanceId, string? environment = null)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return Task.FromResult(false);
            }

            // Abbruch ist in JEDEM Deployment eine reine Store-Operation (keine Aktivitaet laeuft) - daher
            // hier gemeinsam, unabhaengig von der Signal-Variante.
            using WorkflowOperation op = BeginOperation(environment);
            return Task.FromResult(op.Engine.CancelWorkflow(instanceId));
        }

        /// <summary>
        /// Stellt ein Signal an die (existierende, berechtigte) Instanz zu. Die Variante bestimmt, OB dabei
        /// im Web-Prozess advanced wird (inline) oder nur store-only reaktiviert wird (Runner treibt voran).
        /// Die <paramref name="op"/> bleibt bis zum Abschluss dieses Aufrufs gueltig.
        /// </summary>
        protected abstract Task<bool> DeliverSignalAsync(WorkflowOperation op, string instanceId, string signalName);

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
