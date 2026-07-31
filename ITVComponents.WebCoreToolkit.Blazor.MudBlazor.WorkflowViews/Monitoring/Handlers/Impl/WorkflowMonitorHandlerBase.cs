using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Runtime;
using ITVComponents.Workflow.Serialization;
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

        /// <inheritdoc/>
        public async Task<IReadOnlyList<WorkflowStartableDefinition>> ListStartableDefinitionsAsync(
            ClaimsPrincipal user, string? environment = null)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Start }))
            {
                return Array.Empty<WorkflowStartableDefinition>();
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();

            // Der Tenant wird EXPLIZIT gefiltert und nicht dem globalen Query-Filter ueberlassen: ob der
            // ueberhaupt greift, entscheidet die Registrierung des Kontexts im Host (der Weg ueber die
            // DbContext-Factory ist bewusst filterfrei und wuerde die Definitionen ALLER Tenants zeigen).
            // Eine Startauswahl darf davon nicht abhaengen. Tenant-lose Definitionen sind oeffentlich und
            // bleiben startbar - dieselbe Regel wie im Query-Filter der Definitionen.
            string? tenant = CurrentTenant();

            // Nur die hoechste Version je Id - genau die, die ein Start erwischen wuerde. Als korrelierte
            // Unterabfrage, damit EINE Abfrage genuegt (statt Ids holen und je Id nachladen).
            IQueryable<WorkflowDefinitionRow> all = ctx.WorkflowDefinitions.AsNoTracking()
                .Where(r => r.TenantId == null || r.TenantId == tenant);
            var rows = await all
                .Where(r => r.Version == all.Where(o => o.Id == r.Id).Max(o => o.Version))
                .OrderBy(r => r.Id)
                .ToListAsync();

            var result = new List<WorkflowStartableDefinition>(rows.Count);
            foreach (WorkflowDefinitionRow row in rows)
            {
                WorkflowDefinition? def = ReadDefinition(row);
                if (def == null || def.DisabledForStart || !def.StartNodes().Any())
                {
                    // Gesperrte und start-knoten-lose Definitionen gehoeren nicht in eine Startauswahl -
                    // sie wuerden beim Klick nur mit einer Ausnahme quittieren.
                    continue;
                }

                result.Add(new WorkflowStartableDefinition
                {
                    Id = row.Id,
                    Version = row.Version,
                    Name = def.Name
                });
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<WorkflowStartForm?> GetStartFormAsync(ClaimsPrincipal user, string definitionId,
            string? environment = null)
        {
            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Start }))
            {
                return Task.FromResult<WorkflowStartForm?>(null);
            }

            using WorkflowOperation op = BeginOperation(environment);
            // Ohne Version = die hoechste; das ist die, die CreateInstance ebenfalls nimmt.
            WorkflowDefinition? def = op.Store.GetDefinition(definitionId);
            if (def == null)
            {
                LogEnvironment.LogEvent(
                    $"Start-Maske nicht ermittelbar: Definition '{definitionId}' existiert nicht (Umgebung " +
                    $"'{environment ?? "<default>"}').", LogSeverity.Warning);
                return Task.FromResult<WorkflowStartForm?>(null);
            }

            if (!MayStart(def))
            {
                // Wie bei der Liste: nicht dem Query-Filter vertrauen. Sonst genuegte das Erraten einer
                // Definition-Id, um die Maske einer fremden Definition zu sehen.
                LogEnvironment.LogEvent(
                    $"Start-Maske der Definition '{definitionId}' (Tenant '{def.TenantId ?? "<public>"}') " +
                    $"fuer Tenant '{CurrentTenant() ?? "<none>"}' abgelehnt.", LogSeverity.Warning);
                return Task.FromResult<WorkflowStartForm?>(null);
            }

            // Der Editor erzwingt genau EINEN Start-Knoten; aeltere Definitionen koennen mehrere haben.
            // Dann gewinnt der einzige, der ueberhaupt etwas deklariert - sonst der erste.
            var starts = def.StartNodes().ToList();
            StartNode? start = starts.FirstOrDefault(s => s.FormFields is { Count: > 0 }) ?? starts.FirstOrDefault();

            return Task.FromResult<WorkflowStartForm?>(new WorkflowStartForm
            {
                DefinitionId = def.Id,
                Version = def.Version,
                Name = def.Name,
                Description = start?.FormDescription,
                Fields = start?.FormFields?
                             .Where(f => f != null && !string.IsNullOrWhiteSpace(f.Name) && !f.ReadOnly)
                             .ToList()
                         ?? (IReadOnlyList<UserTaskField>)Array.Empty<UserTaskField>(),
                HasSignature = start?.Inputs is { Count: > 0 },
                StrictSignature = start != null && start.Inputs is { Count: > 0 }
                                  && start.ScopeMode == ActivityScopeMode.Replace
            });
        }

        /// <inheritdoc/>
        public Task<WorkflowStartResult> StartInstanceAsync(ClaimsPrincipal user, WorkflowStartRequest request,
            string? environment = null)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DefinitionId))
            {
                LogEnvironment.LogEvent("Workflow-Start ohne Definition-Id abgelehnt.", LogSeverity.Error);
                return Task.FromResult(WorkflowStartResult.Failed("No definition was selected."));
            }

            if (!services.VerifyUserPermissions(new[] { WorkflowSecurity.Start }))
            {
                LogEnvironment.LogEvent(
                    $"Start der Workflow-Definition '{request.DefinitionId}' ohne Berechtigung " +
                    $"'{WorkflowSecurity.Start}' abgelehnt.", LogSeverity.Warning);
                return Task.FromResult(WorkflowStartResult.Failed("You have no permission to start workflows."));
            }

            try
            {
                // Der Tenant, unter dem die Instanz laufen soll. Er muss HIER gesetzt werden: die Engine
                // kennt keinen Tenant-Parameter, und der Store schreibt beim Anlegen
                // 'instance.TenantId ?? ctx.CurrentTenant' fest. Ob der Kontext von sich aus einen Tenant
                // hat, entscheidet aber die Registrierung im Host - der Weg ueber die DbContext-Factory ist
                // bewusst filterfrei und liefert null. Die Instanz waere dann tenant-los, und der
                // Aktivitaets-Host (der seinen Scope aus instance.TenantId oeffnet) faende spaeter kein
                // Plugin ("... im Tenant '(none)' ..."). Derselbe ambiente Scope, den auch der
                // tenant-uebergreifende Runner beim Vortrieb setzt - er gewinnt gegen die Registrierung und
                // deckt zugleich den Inline-Vortrieb ab, der direkt im Anschluss Aktivitaeten ausfuehrt.
                string? tenant = CurrentTenant();
                using IDisposable? tenantScope = string.IsNullOrEmpty(tenant)
                    ? null
                    : WorkflowExecutionScope.UseTenant(tenant);

                using WorkflowOperation op = BeginOperation(environment);

                // Vor dem Anlegen pruefen, WESSEN Definition da gestartet wird - der Dialog liefert zwar nur
                // erlaubte Ids, aber das Erraten einer fremden Id darf nicht genuegen. Kostet einen
                // zusaetzlichen Store-Lesezugriff; ein Start ist selten und die Alternative waere, sich auf
                // die Oberflaeche zu verlassen.
                WorkflowDefinition? def = op.Store.GetDefinition(request.DefinitionId);
                if (def != null && !MayStart(def))
                {
                    LogEnvironment.LogEvent(
                        $"Start der Definition '{request.DefinitionId}' (Tenant " +
                        $"'{def.TenantId ?? "<public>"}') fuer Tenant '{tenant ?? "<none>"}' abgelehnt.",
                        LogSeverity.Warning);
                    return Task.FromResult(WorkflowStartResult.Failed(
                        "This workflow does not belong to your tenant."));
                }

                WorkflowInstance instance = StartInstanceCore(op, request.DefinitionId,
                    request.Variables ?? new Dictionary<string, object>(),
                    string.IsNullOrWhiteSpace(request.CorrelationKey) ? null : request.CorrelationKey);

                if (string.IsNullOrEmpty(instance.TenantId))
                {
                    // Kein Fehler (ein Ein-Mandanten-Host betreibt Workflows legitim tenant-frei), aber die
                    // Ursache spaeterer "kein Plugin im Tenant '(none)'"-Meldungen - deshalb sichtbar.
                    LogEnvironment.LogEvent(
                        $"Workflow '{request.DefinitionId}' wurde OHNE Tenant gestartet (Instanz " +
                        $"'{instance.Id}'): weder IPermissionScope.PermissionPrefix noch der Workflow-Kontext " +
                        "liefern einen Tenant. Tenant-abhaengige Aktivitaeten werden nicht aufloesbar sein.",
                        LogSeverity.Warning);
                }

                // Best-effort Wake wie beim Signal: einen (evtl. im selben Prozess laufenden) Worker sofort
                // auf die frischen Start-Tokens aufmerksam machen, statt ihn bis zum Max-Linger warten zu
                // lassen. Ohne Worker-Betrieb ist der Service nicht registriert -> stiller No-op.
                services.GetService<IWorkflowWorkerWake>()?.Poke(environment, instance.TenantId);
                return Task.FromResult(WorkflowStartResult.Ok(instance.Id));
            }
            catch (Exception ex)
            {
                // Die Engine wirft mit Absicht (Definition fuer den Start gesperrt, Start-Parameter nicht
                // aufloesbar). Der Grund gehoert ins Log UND an den Benutzer - sonst steht er vor einem
                // Dialog, der einfach nichts tut.
                LogEnvironment.LogEvent(
                    $"Konnte Workflow '{request.DefinitionId}' nicht starten (Umgebung " +
                    $"'{environment ?? "<default>"}'): {ex.OutlineException()}", LogSeverity.Error);
                return Task.FromResult(WorkflowStartResult.Failed(ex.Message));
            }
        }

        /// <summary>
        /// Stellt ein Signal an die (existierende, berechtigte) Instanz zu. Die Variante bestimmt, OB dabei
        /// im Web-Prozess advanced wird (inline) oder nur store-only reaktiviert wird (Runner treibt voran).
        /// Die <paramref name="op"/> bleibt bis zum Abschluss dieses Aufrufs gueltig.
        /// </summary>
        protected abstract Task<bool> DeliverSignalAsync(WorkflowOperation op, string instanceId, string signalName);

        /// <summary>
        /// Legt die neue Instanz an. Dieselbe Weggabelung wie bei <see cref="DeliverSignalAsync"/>: inline
        /// wird im Web-Prozess bis zum ersten Wartepunkt advanced, store-only bleibt es bei den aktiven
        /// Start-Tokens, die der Runner aufnimmt. Ausnahmen der Engine reicht die Basis nach oben durch.
        /// </summary>
        protected abstract WorkflowInstance StartInstanceCore(WorkflowOperation op, string definitionId,
            IDictionary<string, object> variables, string? correlationKey);

        /// <summary>
        /// Der Tenant der aktuellen Anfrage - nach derselben Konvention wie in der Aufgaben-Arbeitsliste
        /// (<c>IPermissionScope.PermissionPrefix</c>, klein geschrieben). Null in einem Host ohne
        /// Mandanten-Trennung.
        /// </summary>
        private string? CurrentTenant()
            => services.GetService<IPermissionScope>()?.PermissionPrefix?.ToLower();

        /// <summary>
        /// Darf der aktuelle Tenant diese Definition starten? Tenant-lose Definitionen sind oeffentlich
        /// (dieselbe Regel wie im Query-Filter der Definitionen) und im Kontext jedes Tenants startbar.
        /// </summary>
        private bool MayStart(WorkflowDefinition definition)
            => string.IsNullOrEmpty(definition.TenantId)
               || string.Equals(definition.TenantId, CurrentTenant(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Liest eine Definition aus der Zeile. Fehlerhaftes/aelteres JSON darf die Startauswahl nicht
        /// abbrechen - die betroffene Definition faellt dann heraus, der Grund steht im Log.
        /// </summary>
        private static WorkflowDefinition? ReadDefinition(WorkflowDefinitionRow row)
        {
            try
            {
                return WorkflowJson.Deserialize<WorkflowDefinition>(row.DefinitionJson);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Konnte Definition '{row.Id}' v{row.Version} fuer die Startauswahl nicht lesen: " +
                    $"{ex.OutlineException()}", LogSeverity.Error);
                return null;
            }
        }

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
