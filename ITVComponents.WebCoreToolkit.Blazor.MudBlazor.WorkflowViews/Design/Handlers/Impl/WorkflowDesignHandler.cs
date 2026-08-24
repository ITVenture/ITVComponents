using System;
using System.Collections.Generic;
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
using ITVComponents.Workflow.Activities;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Plugins;
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
    internal sealed class WorkflowDesignHandler : WorkflowHandlerBase, IWorkflowDesignHandler
    {
        private readonly IFreshInjectablePlugin<IInjectableWorkflowActivityCatalog> freshCatalog;

        public WorkflowDesignHandler(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext,
            IFreshInjectablePlugin<IInjectableWorkflowActivityCatalog> freshCatalog)
            : base(services, freshContext)
        {
            this.freshCatalog = freshCatalog;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Design-Operationen brauchen keine Engine - sie lesen und schreiben ausschliesslich ueber den
        /// Store.
        /// </remarks>
        protected override bool NeedsEngine => false;

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
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Design }))
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

                // Oeffentlich ist eine eigene Entscheidung mit eigener Berechtigung - und sie gilt in
                // BEIDE Richtungen: eine bestehende oeffentliche Definition zu aendern wirkt auf alle
                // Mandanten, auch wenn der Speichernde sie gerade auf "eigener Mandant" umstellt.
                // Serverseitig geprueft und nicht nur im Editor: sonst genuegte ein gesetztes Flag im
                // Modell.
                WorkflowDefinition? stored = definition.Key != 0
                    ? op.Store.GetDefinition(definition.Key)
                    : null;
                bool touchesPublic = definition.IsPublic || (stored?.IsPublic ?? false);
                if (touchesPublic && !Services.VerifyUserPermissions(new[] { WorkflowSecurity.DesignPublic }))
                {
                    LogEnvironment.LogEvent(
                        $"Speichern der oeffentlichen Workflow-Definition '{definition.Id}' ohne "
                        + $"Berechtigung '{WorkflowSecurity.DesignPublic}' abgelehnt.", LogSeverity.Warning);
                    return Task.FromResult(false);
                }

                // Und die zweite Frage, die der Public-Riegel NICHT beantwortet: gehoert die bestehende
                // Zeile ueberhaupt diesem Mandanten? Der Schluessel kommt aus dem geposteten Modell, und
                // der Store trifft die Zeile darueber ausdruecklich filterfrei - ohne diesen Guard
                // landete ein fremder Key auf der Definition seines Besitzers, samt neu gesetztem
                // Mandanten.
                if (!MayTouchDefinition(stored, "Speichern"))
                {
                    return Task.FromResult(false);
                }

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

        /// <inheritdoc/>
        public Task<IReadOnlyList<ActivityTypeInfo>> GetActivityTypesAsync(ClaimsPrincipal user, string? environment,
            string? executionTarget)
            => Task.FromResult(WithCatalog(environment, executionTarget,
                c => c.GetActivityTypes(), (IReadOnlyList<ActivityTypeInfo>)Array.Empty<ActivityTypeInfo>()));

        /// <inheritdoc/>
        public Task<IReadOnlyList<ActivityParameter>> GetActivityParametersAsync(ClaimsPrincipal user,
            string? environment, string? executionTarget, string activityRef)
            => Task.FromResult(WithCatalog(environment, executionTarget,
                c => c.GetParameters(activityRef), (IReadOnlyList<ActivityParameter>)Array.Empty<ActivityParameter>()));

        /// <inheritdoc/>
        public Task<IReadOnlyList<ActivityParameterValue>> GetActivityValidValuesAsync(ClaimsPrincipal user,
            string? environment, string? executionTarget, string activityRef, string parameterName)
            => Task.FromResult(WithCatalog(environment, executionTarget,
                c => c.GetValidValues(activityRef, parameterName),
                (IReadOnlyList<ActivityParameterValue>)Array.Empty<ActivityParameterValue>()));

        /// <summary>
        /// Loest den Instanz-Katalog fuer (Umgebung, ExecutionTarget) auf und fuehrt EINE Abfrage aus. Der
        /// Katalog wird pro Abfrage frisch geleast und danach freigegeben (er haelt eine Factory-Referenz und
        /// reflektiert live). Ohne passenden Instanz-Katalog-Namen wird der per DI registrierte Default-Katalog
        /// geleast (Lease(null) -> DefaultPluginInjector). Ist ein benannter Katalog nicht aufloesbar, wird -
        /// protokolliert - ebenfalls auf den Default zurueckgefallen.
        /// </summary>
        private T WithCatalog<T>(string? environment, string? executionTarget,
            Func<IWorkflowActivityCatalog, T> query, T fallback)
        {
            string? catalogName = WorkflowEnvironmentResolver.ActivityCatalogPluginName(Services, environment, executionTarget);
            IPluginLease<IInjectableWorkflowActivityCatalog>? lease = null;
            try
            {
                lease = freshCatalog.Lease(catalogName);
                if (lease.Value == null && catalogName != null)
                {
                    // Benannter Instanz-Katalog nicht aufloesbar -> Default-Katalog verwenden.
                    LogEnvironment.LogEvent(
                        $"ActivityCatalog-Plugin '{catalogName}' konnte nicht aufgeloest werden - der Default-Katalog wird verwendet.",
                        LogSeverity.Warning);
                    lease.Dispose();
                    lease = freshCatalog.Lease(null);
                }

                return lease.Value != null ? query(lease.Value) : fallback;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Katalog-Abfrage fuer Umgebung '{environment}'/Ziel '{executionTarget}' fehlgeschlagen: {ex.OutlineException()}",
                    LogSeverity.Error);
                return fallback;
            }
            finally
            {
                lease?.Dispose();
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
                FlowCount = flowCount,
                // Aus der SPALTE, nicht aus dem JSON: sie ist die Wahrheit ueber die Zugehoerigkeit.
                TenantId = row.TenantId,
                IsPublic = row.TenantId == null
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
