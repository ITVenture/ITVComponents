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
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Model;
using ITVComponents.Workflow.Retention;
using ITVComponents.Workflow.Runtime;
using ITVComponents.Workflow.Serialization;
using ITVComponents.Workflow.Stores;
using ITVComponents.Workflow.WebWorker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

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
    internal abstract class WorkflowMonitorHandlerBase : WorkflowHandlerBase, IWorkflowMonitorHandler
    {
        protected WorkflowMonitorHandlerBase(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
            : base(services, freshContext)
        {
        }

        /// <inheritdoc/>
        public async Task<PagedResult<WorkflowInstanceListItem>> ListInstancesAsync(ClaimsPrincipal user, WorkflowListQuery query,
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
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return false;
            }

            using WorkflowOperation op = BeginOperation(environment);
            if (!TryLoadOwnInstance(op, instanceId, "Signal an", out WorkflowInstance instance))
            {
                return false;
            }

            bool delivered = await DeliverSignalAsync(op, instanceId, signalName);
            if (delivered)
            {
                // Best-effort Wake: einen (evtl. im selben Prozess laufenden) Worker sofort auf diesen Tenant/
                // diese Umgebung aufmerksam machen, statt ihn bis zum Max-Linger warten zu lassen. Fehlt der
                // Worker (kein Split-/Worker-Betrieb), ist der Service nicht registriert -> stiller No-op.
                Services.GetService<IWorkflowWorkerWake>()?.Poke(environment, instance.TenantId);
            }

            return delivered;
        }

        /// <inheritdoc/>
        public Task<bool> CancelAsync(ClaimsPrincipal user, string instanceId, string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return Task.FromResult(false);
            }

            // Abbruch ist in JEDEM Deployment eine reine Store-Operation (keine Aktivitaet laeuft) - daher
            // hier gemeinsam, unabhaengig von der Signal-Variante.
            using WorkflowOperation op = BeginOperation(environment);
            if (!TryLoadOwnInstance(op, instanceId, "Abbruch", out _))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(op.Engine.CancelWorkflow(instanceId));
        }

        /// <inheritdoc/>
        public Task<bool> SetSuspendedAsync(ClaimsPrincipal user, string instanceId, bool suspend,
            string? reason = null, string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                LogEnvironment.LogEvent(
                    $"{(suspend ? "Suspend" : "Resume")} der Instanz '{instanceId}': Berechtigung "
                    + $"'{WorkflowSecurity.Operate}' abgelehnt.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            // Wie der Abbruch eine reine Store-Operation: es laeuft nichts, was koordiniert werden muesste.
            using WorkflowOperation op = BeginOperation(environment);
            if (!TryLoadOwnInstance(op, instanceId, suspend ? "Anhalten" : "Fortsetzen", out _))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(suspend
                ? op.Engine.SuspendWorkflow(instanceId, reason, UserName(user))
                : op.Engine.ResumeWorkflow(instanceId, UserName(user)));
        }

        /// <inheritdoc/>
        public Task<bool> SetPriorityAsync(ClaimsPrincipal user, string instanceId, int priority,
            string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                LogEnvironment.LogEvent(
                    $"Prioritaets-Aenderung an Instanz '{instanceId}' ohne Berechtigung " +
                    $"'{WorkflowSecurity.Operate}' abgelehnt.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            using WorkflowOperation op = BeginOperation(environment);
            if (!TryLoadOwnInstance(op, instanceId, "Prioritaets-Aenderung an", out _))
            {
                return Task.FromResult(false);
            }

            // Wie der Abbruch eine reine Store-Operation - die Instanz laeuft weiter, nur die Reihenfolge
            // ihres Aufgriffs aendert sich. Ein verlorenes Rennen gegen einen laufenden Zweig meldet die
            // Engine mit false (und protokolliert den Grund); der Benutzer kann es dann wiederholen.
            return Task.FromResult(op.Engine.SetPriority(instanceId, priority));
        }

        /// <inheritdoc/>
        public Task<WorkflowRetryInfo?> GetRetryInfoAsync(ClaimsPrincipal user, string instanceId,
            string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return Task.FromResult<WorkflowRetryInfo?>(null);
            }

            using WorkflowOperation op = BeginOperation(environment);
            if (!TryLoadOwnInstance(op, instanceId, "Retry-Info fuer", out WorkflowInstance instance))
            {
                return Task.FromResult<WorkflowRetryInfo?>(null);
            }

            if (instance.Status != WorkflowStatus.Faulted)
            {
                return Task.FromResult<WorkflowRetryInfo?>(new WorkflowRetryInfo
                {
                    InstanceId = instance.Id,
                    FaultMessage = instance.FaultMessage,
                    CanRetry = false,
                    Reason = $"This instance is {instance.Status}, not faulted - there is nothing to retry."
                });
            }

            IReadOnlyList<Token> stalled = WorkflowEngine.FindStalledBranches(instance);
            if (stalled.Count == 0)
            {
                return Task.FromResult<WorkflowRetryInfo?>(new WorkflowRetryInfo
                {
                    InstanceId = instance.Id,
                    FaultMessage = instance.FaultMessage,
                    CanRetry = false,
                    Reason = "The failure is not tied to a step, so there is no point to resume from. "
                             + "See the fault message and the history."
                });
            }

            // Die Definition EINMAL laden - sonst ein Store-Zugriff je Zweig.
            WorkflowDefinition? definition = LoadDefinition(op, instance);
            var branches = stalled.Select(t =>
            {
                HistoryEntry? fault = instance.History?
                    .LastOrDefault(h => string.Equals(h.Event, "Faulted", StringComparison.Ordinal)
                                        && h.NodeId == t.NodeId);
                return new WorkflowRetryBranch
                {
                    TokenId = t.Id,
                    NodeId = t.NodeId,
                    NodeName = NodeName(definition, t.NodeId),
                    Faulted = fault != null,
                    FaultMessage = fault?.Detail,
                    FailedUtc = fault?.TimestampUtc,
                    // Genau die Werte, die DIESER Zweig beim naechsten Versuch liest - in einer parallelen
                    // Region sein eigener Scope, sonst der Instanz-Scope.
                    Variables = WorkflowEngine.ScopeOf(instance, t)
                        .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(p => new WorkflowRetryVariable
                        {
                            Name = p.Key,
                            Value = WorkflowVariableValue.Display(p.Value),
                            Kind = WorkflowVariableValue.KindOf(p.Value),
                            TypeName = p.Value?.GetType().Name
                        })
                        .ToList()
                };
            }).ToList();

            return Task.FromResult<WorkflowRetryInfo?>(new WorkflowRetryInfo
            {
                InstanceId = instance.Id,
                FaultMessage = instance.FaultMessage,
                CanRetry = true,
                Branches = branches
            });
        }

        /// <inheritdoc/>
        public Task<WorkflowRetryResult> RetryAsync(ClaimsPrincipal user, string instanceId,
            IDictionary<string, IDictionary<string, object?>>? branchUpdates = null, string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                LogEnvironment.LogEvent(
                    $"Wiederaufnahme der Instanz '{instanceId}' ohne Berechtigung " +
                    $"'{WorkflowSecurity.Operate}' abgelehnt.", LogSeverity.Warning);
                return Task.FromResult(WorkflowRetryResult.Failed(
                    "You have no permission to operate workflow instances."));
            }

            try
            {
                using WorkflowOperation op = BeginOperation(environment);
                if (!TryLoadOwnInstance(op, instanceId, "Wiederaufnahme", out WorkflowInstance instance))
                {
                    return Task.FromResult(WorkflowRetryResult.Failed("This instance does not exist."));
                }

                // Unter dem Tenant DER INSTANZ arbeiten - nicht dem der Anfrage. Beim Inline-Betrieb laeuft
                // gleich danach die Aktivitaet, und die muss die Daten ihres eigenen Mandanten sehen.
                using IDisposable? tenantScope = string.IsNullOrEmpty(instance.TenantId)
                    ? null
                    : WorkflowExecutionScope.UseTenant(instance.TenantId);

                // Je Zweig (Token-Id) ein eigener Satz Korrekturen - nach einem Split hat jeder Zweig
                // seinen eigenen Scope, eine Korrektur im einen erreicht den anderen nicht.
                var updates = branchUpdates?.ToDictionary(
                    b => b.Key,
                    b => (IDictionary<string, object>)b.Value.ToDictionary(
                        p => p.Key, p => p.Value!, StringComparer.Ordinal),
                    StringComparer.Ordinal);

                string? who = user?.Identity?.Name;
                if (!op.Engine.RetryFaultedBranches(instanceId, updates,
                        string.IsNullOrEmpty(who) ? null : $"resumed by {who}"))
                {
                    return Task.FromResult(WorkflowRetryResult.Failed("This instance does not exist."));
                }

                // Weiter geht es wie ueberall: inline im Web-Prozess oder store-only durch den Runner.
                ResumeAfterRetry(op, instanceId);
                Services.GetService<IWorkflowWorkerWake>()?.Poke(environment, instance.TenantId);
                return Task.FromResult(WorkflowRetryResult.Ok());
            }
            catch (Exception ex)
            {
                // Die Engine wirft mit Absicht (nicht fehlgeschlagen, kein Wiederaufsatzpunkt). Der Grund
                // gehoert ins Log UND an den Benutzer.
                LogEnvironment.LogEvent(
                    $"Konnte Instanz '{instanceId}' nicht wieder aufnehmen (Umgebung " +
                    $"'{environment ?? "<default>"}'): {ex.OutlineException()}", LogSeverity.Error);
                return Task.FromResult(WorkflowRetryResult.Failed(ex.Message));
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<WorkflowStartableDefinition>> ListStartableDefinitionsAsync(
            ClaimsPrincipal user, string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Start }))
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

                if (!MayUse(def))
                {
                    // Dasselbe Praedikat, das der Start gleich noch einmal anlegt (MayStart). Wer das
                    // verlangte Feature oder die verlangte Berechtigung nicht hat, soll die Definition
                    // gar nicht erst in der Auswahl sehen.
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
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Start }))
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
                                  && start.ScopeMode == ActivityScopeMode.Replace,
                // Die festen Werte des Zeitplans - damit ein Start von Hand nicht anders losläuft als der
                // zeitgesteuerte. Bewusst von ALLEN Start-Knoten eingesammelt und nicht nur von dem, der
                // die Maske stellt: aeltere Definitionen koennen mehrere haben, und ein Wert, der dann
                // unter den Tisch fiele, waere genau die stille Abweichung, die hier verhindert werden soll.
                ScheduleDefaults = starts
                    .Where(s => s.ScheduleStart?.Variables is { Count: > 0 })
                    .SelectMany(s => s.ScheduleStart.Variables)
                    .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (object?)g.First().Value, StringComparer.OrdinalIgnoreCase)
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

            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Start }))
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
                if (def == null)
                {
                    // Frueher lief dieser Fall in den Start hinein und starb dort an einer Exception. Der
                    // Grund gehoert hierher, wo er noch bekannt ist.
                    LogEnvironment.LogEvent(
                        $"Start abgelehnt: Definition '{request.DefinitionId}' existiert nicht (Umgebung " +
                        $"'{environment ?? "<default>"}', Tenant '{tenant ?? "<none>"}').", LogSeverity.Warning);
                    return Task.FromResult(WorkflowStartResult.Failed("This workflow does not exist."));
                }

                if (!MayStart(def))
                {
                    LogEnvironment.LogEvent(
                        $"Start der Definition '{request.DefinitionId}' (Tenant " +
                        $"'{def.TenantId ?? "<public>"}') fuer Tenant '{tenant ?? "<none>"}' abgelehnt.",
                        LogSeverity.Warning);
                    return Task.FromResult(WorkflowStartResult.Failed(
                        "This workflow does not belong to your tenant."));
                }

                // Ueber den KEY der eben geprueften Definition, nicht noch einmal ueber den Namen. Sonst
                // stuenden hier zwei unabhaengige Aufloesungen desselben Namens: geprueft wuerde die eine,
                // gestartet die andere - es genuegte, dass zwischen beiden Lesezugriffen eine
                // mandanteneigene Fassung gleichen Namens entsteht, denn die schlaegt die oeffentliche.
                WorkflowInstance instance = StartInstanceCore(op, def.Key,
                    request.Variables ?? new Dictionary<string, object>(),
                    string.IsNullOrWhiteSpace(request.CorrelationKey) ? null : request.CorrelationKey,
                    request.Priority);

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
                Services.GetService<IWorkflowWorkerWake>()?.Poke(environment, instance.TenantId);
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
        /// <remarks>
        /// Bewusst die <b>technische</b> Kennung: der Aufrufer hat die Definition bereits aufgeloest und
        /// geprueft, und der Start soll genau die nehmen - nicht das, was der Name im Augenblick des
        /// Starts gerade bedeutet.
        /// </remarks>
        protected abstract WorkflowInstance StartInstanceCore(WorkflowOperation op, int definitionKey,
            IDictionary<string, object> variables, string? correlationKey, int? priority);

        /// <summary>
        /// Treibt die eben wieder aufgenommene Instanz weiter. Dieselbe Weggabelung wie bei Signal und
        /// Start: inline wird im Web-Prozess advanced, store-only nimmt der Runner den nun aktiven Zweig auf.
        /// </summary>
        protected abstract void ResumeAfterRetry(WorkflowOperation op, string instanceId);

        /// <summary>
        /// Laedt die Definition einer Instanz. Sie wird nur fuer Anzeigenamen gebraucht - scheitert das,
        /// funktioniert die Maske mit den Knoten-Ids weiter, der Grund muss aber ins Log (eine nicht
        /// ladbare Definition ist selten harmlos).
        /// </summary>
        private static WorkflowDefinition? LoadDefinition(WorkflowOperation op, WorkflowInstance instance)
        {
            try
            {
                // Ueber die technische Kennung: die Instanz verweist auf GENAU ihre Definition. Ueber
                // Name+Version entschiede der aktive Mandant mit, und die Maske zeigte im Zweifel die
                // Knotennamen eines anderen Graphen.
                return op.Store.GetDefinition(instance.DefinitionKey);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Konnte die Definition '{instance.DefinitionId}' v{instance.DefinitionVersion} der Instanz " +
                    $"'{instance.Id}' nicht laden: {ex.OutlineException()}", LogSeverity.Warning);
                return null;
            }
        }

        /// <summary>Der Anzeigename eines Knotens, oder die Id.</summary>
        private static string? NodeName(WorkflowDefinition? definition, string? nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return null;
            }

            WorkflowNode? node = definition?.GetNode(nodeId);
            return string.IsNullOrEmpty(node?.Name) ? nodeId : node!.Name;
        }

        /// <summary>
        /// Darf der aktuelle Tenant diese Definition starten? Tenant-lose Definitionen sind oeffentlich
        /// (dieselbe Regel wie im Query-Filter der Definitionen) und im Kontext jedes Tenants startbar.
        /// </summary>
        /// <inheritdoc/>
        public Task<IReadOnlyList<CentralWorkflowItem>> ListCentralWorkflowsAsync(ClaimsPrincipal user,
            string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return Task.FromResult<IReadOnlyList<CentralWorkflowItem>>(
                    Array.Empty<CentralWorkflowItem>());
            }

            string? tenant = CurrentTenant();
            using WorkflowOperation op = BeginOperation(environment);

            IReadOnlyList<WorkflowStartTrigger> offered = op.Store.FindActivatableTriggers(tenant);
            IReadOnlyList<WorkflowStartTriggerActivation> mine = op.Store.GetActivations(tenant);

            // Beide Arten. Der Nachrichten-Einstieg war hier lange ausgeblendet, weil eine eintreffende
            // Nachricht in fuenfzig Mandanten je einen Vorgang eroeffnen konnte - genau das hat der
            // Ursprungs-Mandant beseitigt: es feuern nur die Aktivierungen DIESES Mandanten. Das
            // Fan-out gibt es nur noch ohne Ursprung, und dafuer steht AllowTenantlessStart am Knoten.
            var result = new List<CentralWorkflowItem>();
            foreach (WorkflowStartTrigger trigger in offered)
            {
                // Feature und Berechtigung der Definition - was der Mandant nicht verwenden darf, steht
                // ihm auch nicht zum Anhaken. Beides steht denormalisiert am Ausloeser, damit die Liste
                // nicht je Zeile ein Definitions-JSON auspacken muss.
                if (!MayUseGate(trigger.RequiredFeature, trigger.RequiredPermission))
                {
                    continue;
                }

                WorkflowStartTriggerActivation? activation = mine.FirstOrDefault(
                    a => a.OwnerTenantId == trigger.TenantId && a.DefinitionId == trigger.DefinitionId
                         && a.NodeId == trigger.NodeId && a.Kind == trigger.Kind);

                bool isSchedule = trigger.Kind == WorkflowStartTriggerKind.Schedule;
                bool ownPattern = isSchedule && trigger.AllowReschedule
                                  && !string.IsNullOrWhiteSpace(activation?.PatternOverride);
                result.Add(new CentralWorkflowItem
                {
                    DefinitionId = trigger.DefinitionId,
                    NodeId = trigger.NodeId,
                    Name = DefinitionName(op, trigger),
                    Kind = trigger.Kind,
                    SignalName = isSchedule ? null : trigger.SignalName,
                    Pattern = ownPattern ? activation!.PatternOverride : trigger.Pattern,
                    CentralPattern = trigger.Pattern,
                    Enabled = activation?.Enabled == true,
                    // Ein eigenes Muster gibt es nur beim Zeitplan - bei einer Nachricht gaebe es
                    // nichts umzustellen, und der Stift daneben waere ein Knopf ohne Wirkung.
                    MayReschedule = isSchedule && trigger.AllowReschedule,
                    PatternOverride = isSchedule ? activation?.PatternOverride : null,
                    NextDueUtc = activation?.NextDueUtc,
                    LastRunUtc = activation?.LastRunUtc,
                    LastInstanceId = activation?.LastInstanceId
                });
            }

            // Die Waisen: uebernommen, aber der Ausloeser dazu ist weggefallen. Sie erscheinen bewusst
            // mit - eine Uebernahme, die ab jetzt schweigt, darf nicht einfach aus der Liste
            // verschwinden.
            //
            // Die Art gehoert in den Vergleich: EIN Start-Knoten kann beides deklarieren (eine Nachricht
            // UND einen Zeitplan). Ohne sie hielte der noch vorhandene Zeitplan die Nachrichten-Uebernahme
            // desselben Knotens faelschlich fuer lebendig - und die Waise verschwaende lautlos, also genau
            // das, was dieser Block verhindern soll.
            foreach (WorkflowStartTriggerActivation orphan in mine.Where(a =>
                         offered.All(t => t.DefinitionId != a.DefinitionId || t.NodeId != a.NodeId
                                          || t.Kind != a.Kind)))
            {
                result.Add(new CentralWorkflowItem
                {
                    DefinitionId = orphan.DefinitionId,
                    NodeId = orphan.NodeId,
                    Kind = orphan.Kind,
                    Enabled = orphan.Enabled,
                    LastRunUtc = orphan.LastRunUtc,
                    LastInstanceId = orphan.LastInstanceId,
                    Orphaned = true
                });
            }

            return Task.FromResult<IReadOnlyList<CentralWorkflowItem>>(
                result.OrderBy(r => r.Name ?? r.DefinitionId).ToList());
        }

        /// <inheritdoc/>
        public Task<bool> SetCentralWorkflowActivationAsync(ClaimsPrincipal user,
            CentralWorkflowActivationRequest request, string? environment = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                LogEnvironment.LogEvent(
                    $"Uebernahme von '{request.DefinitionId}' abgelehnt: '{WorkflowSecurity.Operate}' fehlt.",
                    LogSeverity.Warning);
                return Task.FromResult(false);
            }

            string? tenant = CurrentTenant();
            using WorkflowOperation op = BeginOperation(environment);

            // Nicht der Oberflaeche glauben: sie liefert zwar nur erlaubte Zeilen, aber das Erraten einer
            // Definition-Id darf nicht genuegen, um einen zentralen Ablauf scharf zu schalten.
            WorkflowStartTrigger? trigger = op.Store.FindActivatableTriggers(tenant).FirstOrDefault(
                t => t.Kind == request.Kind
                     && t.DefinitionId == request.DefinitionId && t.NodeId == request.NodeId);
            if (trigger == null)
            {
                LogEnvironment.LogEvent(
                    $"Uebernahme abgelehnt: '{request.DefinitionId}' (Knoten '{request.NodeId}') wird dem "
                    + $"Mandanten '{tenant ?? "<none>"}' gar nicht angeboten.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            if (!MayUseGate(trigger.RequiredFeature, trigger.RequiredPermission))
            {
                LogEnvironment.LogEvent(
                    $"Uebernahme von '{request.DefinitionId}' abgelehnt: Feature "
                    + $"'{trigger.RequiredFeature ?? "-"}' bzw. Berechtigung "
                    + $"'{trigger.RequiredPermission ?? "-"}' fehlt.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            bool isSchedule = trigger.Kind == WorkflowStartTriggerKind.Schedule;
            string? ownPattern = isSchedule ? request.PatternOverride : null;
            if (!string.IsNullOrWhiteSpace(request.PatternOverride) && !isSchedule)
            {
                // Ein Muster an einem Nachrichten-Einstieg ergibt keinen Sinn - es gibt keinen Termin,
                // den es verstellen koennte. Verworfen und gesagt, aus demselben Grund wie unten.
                LogEnvironment.LogEvent(
                    $"Eigenes Muster fuer '{request.DefinitionId}' (Knoten '{request.NodeId}') verworfen: "
                    + "der Einstieg ist eine Nachricht und hat keinen Zeitplan.", LogSeverity.Warning);
            }

            if (!string.IsNullOrWhiteSpace(ownPattern) && !trigger.AllowReschedule)
            {
                // Verworfen statt uebernommen - und gesagt: sonst stellt jemand einen Termin ein, sieht
                // ihn nirgends wieder und sucht beim Runner.
                LogEnvironment.LogEvent(
                    $"Eigenes Muster fuer '{request.DefinitionId}' (Knoten '{request.NodeId}') verworfen: "
                    + "der Start-Knoten erlaubt es nicht. Es gilt die zentrale Vorgabe.",
                    LogSeverity.Warning);
                ownPattern = null;
            }

            var activation = new WorkflowStartTriggerActivation
            {
                OwnerTenantId = trigger.TenantId,
                DefinitionId = trigger.DefinitionId,
                NodeId = trigger.NodeId,
                Kind = trigger.Kind,
                TenantId = tenant,
                Enabled = request.Enabled,
                PatternOverride = ownPattern,
                ActivatedBy = user?.Identity?.Name,
                ActivatedUtc = DateTime.UtcNow
            };

            // Die erste Faelligkeit nur beim ANHAKEN setzen - und nur, wenn es noch keine gibt: der Store
            // laesst den Lauf-Zustand einer bestehenden Zeile sonst unangetastet. Genau deshalb loescht
            // das Abhaken nicht, sondern deaktiviert.
            //
            // Nur beim Zeitplan: eine Nachrichten-Aktivierung hat keine Faelligkeit (NextDueUtc bleibt
            // null), und das ist zugleich, was sie aus dem Aufgriff des Runners heraushaelt.
            if (request.Enabled && isSchedule)
            {
                string effective = ownPattern ?? trigger.Pattern;
                bool known = op.Store.GetActivations(tenant).Any(
                    a => a.OwnerTenantId == trigger.TenantId && a.DefinitionId == trigger.DefinitionId
                         && a.NodeId == trigger.NodeId && a.Kind == trigger.Kind);
                if (!known)
                {
                    activation.NextDueUtc = WorkflowStartTriggerFactory.FirstDueUtc(effective,
                        DateTime.UtcNow,
                        $"'{trigger.DefinitionId}', Knoten '{trigger.NodeId}', Mandant '{tenant ?? "-"}'");
                }
            }

            op.Store.SaveActivation(activation);
            LogEnvironment.LogEvent(
                $"Zentraler Ablauf '{trigger.DefinitionId}' (Knoten '{trigger.NodeId}') wurde von "
                + $"'{user?.Identity?.Name ?? "?"}' fuer Mandant '{tenant ?? "<none>"}' "
                + $"{(request.Enabled ? "uebernommen" : "abgegeben")}.", LogSeverity.Report);
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<RetentionSettingItem>> ListRetentionSettingsAsync(ClaimsPrincipal user,
            string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                return Task.FromResult<IReadOnlyList<RetentionSettingItem>>(
                    Array.Empty<RetentionSettingItem>());
            }

            string? tenant = CurrentTenant();
            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();

            // MIT Query-Filter: der zieht hier genau die richtige Grenze (eigene Definitionen plus die
            // oeffentlichen). Je fachlicher Identitaet nur die HOECHSTE Version - der Widerspruch gehoert
            // der Definition als Ganzem, nicht einer ihrer Fassungen.
            List<WorkflowDefinitionRow> newest = ctx.WorkflowDefinitions.AsNoTracking()
                .ToList()
                .GroupBy(d => new { d.TenantId, d.Id })
                .Select(g => g.OrderByDescending(d => d.Version).First())
                .ToList();

            // Hier MUSS je Zeile das Definitions-JSON ausgepackt werden - anders als bei den Ausloesern,
            // wo die Gate-Felder denormalisiert danebenstehen. Die Fristen liegen ausschliesslich im
            // JSON (die Definitionszeile hat fuenf Spalten), und ohne sie gaebe es nichts anzuzeigen.
            IReadOnlyList<WorkflowRetentionOverride> mine = op.Store.GetRetentionOverrides(tenant);
            WorkflowRetentionDefaults? defaults = Services.GetService<WorkflowRetentionDefaults>();

            var result = new List<RetentionSettingItem>();
            foreach (WorkflowDefinitionRow row in newest)
            {
                WorkflowDefinition? definition =
                    WorkflowJson.Deserialize<WorkflowDefinition>(row.DefinitionJson);
                if (definition == null)
                {
                    // Eine Definition, die sich nicht lesen laesst, ist ein eigener Befund - nicht eine
                    // Zeile, die kommentarlos fehlt.
                    LogEnvironment.LogEvent(
                        $"Aufbewahrungs-Uebersicht: Definition '{row.Id}' v{row.Version} liess sich nicht "
                        + "lesen und fehlt in der Liste.", LogSeverity.Warning);
                    continue;
                }

                WorkflowRetentionOverride? objection = mine.FirstOrDefault(
                    o => o.OwnerTenantId == row.TenantId && o.DefinitionId == row.Id);

                result.Add(new RetentionSettingItem
                {
                    DefinitionId = row.Id,
                    Name = definition.Name,
                    IsPublic = row.TenantId == null,
                    MayObject = definition.AllowTenantRetentionOverride,
                    Archive = RetentionValueItem.From(
                        WorkflowRetentionPolicy.Archive(definition, objection, defaults)),
                    Attachments = RetentionValueItem.From(
                        WorkflowRetentionPolicy.Attachments(definition, objection, defaults)),
                    MyRetentionDays = objection?.RetentionDays,
                    MyAttachmentRetentionDays = objection?.AttachmentRetentionDays,
                    MinRetentionDays = definition.MinTenantRetentionDays,
                    MaxRetentionDays = definition.MaxTenantRetentionDays,
                    MinAttachmentRetentionDays = definition.MinTenantAttachmentRetentionDays,
                    MaxAttachmentRetentionDays = definition.MaxTenantAttachmentRetentionDays,
                    SetBy = objection?.SetBy,
                    SetUtc = objection?.SetUtc == default ? null : objection?.SetUtc
                });
            }

            return Task.FromResult<IReadOnlyList<RetentionSettingItem>>(
                result.OrderBy(r => r.Name ?? r.DefinitionId).ThenBy(r => r.IsPublic).ToList());
        }

        /// <inheritdoc/>
        public Task<bool> SetRetentionObjectionAsync(ClaimsPrincipal user,
            RetentionObjectionRequest request, string? environment = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Operate }))
            {
                LogEnvironment.LogEvent(
                    $"Widerspruch gegen die Fristen von '{request.DefinitionId}' abgelehnt: "
                    + $"'{WorkflowSecurity.Operate}' fehlt.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            string? tenant = CurrentTenant();
            using WorkflowOperation op = BeginOperation(environment);

            // Nicht der Oberflaeche glauben: der Besitzer wird hier bestimmt, nicht uebernommen - er ist
            // Teil der Identitaet des Widerspruchs, und das Erraten einer Definition-Id darf nicht
            // genuegen, um eine Frist an einer fremden Definition zu setzen.
            string? owner = request.IsPublic ? null : tenant;
            if (op.Store.ResolveDefinitionKey(owner, request.DefinitionId) == null)
            {
                LogEnvironment.LogEvent(
                    $"Widerspruch abgelehnt: eine {(request.IsPublic ? "oeffentliche" : "eigene")} "
                    + $"Definition '{request.DefinitionId}' gibt es fuer den Mandanten "
                    + $"'{tenant ?? "<none>"}' nicht.", LogSeverity.Warning);
                return Task.FromResult(false);
            }

            op.Store.SaveRetentionOverride(new WorkflowRetentionOverride
            {
                OwnerTenantId = owner,
                DefinitionId = request.DefinitionId,
                TenantId = tenant,
                RetentionDays = request.RetentionDays,
                AttachmentRetentionDays = request.AttachmentRetentionDays,
                SetBy = user?.Identity?.Name,
                SetUtc = DateTime.UtcNow
            });

            bool withdrawn = request.RetentionDays == null && request.AttachmentRetentionDays == null;
            LogEnvironment.LogEvent(
                $"Die Aufbewahrungsfrist zu '{request.DefinitionId}' wurde von "
                + $"'{user?.Identity?.Name ?? "?"}' fuer Mandant '{tenant ?? "<none>"}' "
                + (withdrawn
                    ? "zurueckgenommen - es gilt wieder die Vorgabe."
                    : $"auf {request.RetentionDays?.ToString() ?? "-"} bzw. "
                      + $"{request.AttachmentRetentionDays?.ToString() ?? "-"} Tage gesetzt."),
                LogSeverity.Report);
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public async Task<PagedResult<ArchivedInstanceListItem>> ListArchivedInstancesAsync(
            ClaimsPrincipal user, WorkflowListQuery query, string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Monitor }))
            {
                return new PagedResult<ArchivedInstanceListItem>();
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();

            // MIT Query-Filter, ausdruecklich: die Archiv-Tabelle traegt seit dem Aufbewahrungslauf
            // einen Mandanten-Filter, weil ein archivierter Vorgang genauso einem Mandanten gehoert wie
            // ein lebender. Die Laeufe, die mandantenuebergreifend raeumen muessen, setzen dagegen
            // IgnoreQueryFilters() - und zwar sie, nicht diese Ansicht.
            IQueryable<WorkflowArchivedInstanceRow> q = ctx.WorkflowArchivedInstances.AsNoTracking();

            if (query.Status.HasValue)
            {
                int status = query.Status.Value;
                q = q.Where(r => r.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string term = query.Search!;
                q = q.Where(r => r.InstanceId.Contains(term) || r.DefinitionId.Contains(term)
                                 || (r.DefinitionName != null && r.DefinitionName.Contains(term))
                                 || (r.FaultCode != null && r.FaultCode.Contains(term)));
            }

            int total = await q.CountAsync();

            // Vorgabe ist das Ende, absteigend: was zuletzt geendet hat, sucht man zuerst. Nach dem
            // Archivierungs-Zeitpunkt zu sortieren waere die Reihenfolge des Aufraeum-Laufs, nicht die
            // des Geschehens.
            q = query.SortColumn switch
            {
                nameof(ArchivedInstanceListItem.CreatedUtc) => query.SortDescending
                    ? q.OrderByDescending(r => r.CreatedUtc)
                    : q.OrderBy(r => r.CreatedUtc),
                nameof(ArchivedInstanceListItem.ArchivedUtc) => query.SortDescending
                    ? q.OrderByDescending(r => r.ArchivedUtc)
                    : q.OrderBy(r => r.ArchivedUtc),
                nameof(ArchivedInstanceListItem.DefinitionId) => query.SortDescending
                    ? q.OrderByDescending(r => r.DefinitionId)
                    : q.OrderBy(r => r.DefinitionId),
                _ => query.SortDescending || string.IsNullOrEmpty(query.SortColumn)
                    ? q.OrderByDescending(r => r.EndedUtc)
                    : q.OrderBy(r => r.EndedUtc)
            };

            List<WorkflowArchivedInstanceRow> rows = await q
                .Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
            return new PagedResult<ArchivedInstanceListItem>
            {
                Items = rows.Select(ToArchivedListItem).ToList(),
                TotalCount = total
            };
        }

        /// <inheritdoc/>
        public async Task<ArchivedInstanceDetail?> GetArchivedInstanceAsync(ClaimsPrincipal user,
            string instanceId, string? environment = null)
        {
            if (!Services.VerifyUserPermissions(new[] { WorkflowSecurity.Monitor }))
            {
                return null;
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            WorkflowArchivedInstanceRow? row = await ctx.WorkflowArchivedInstances.AsNoTracking()
                .FirstOrDefaultAsync(r => r.InstanceId == instanceId);
            if (row == null)
            {
                return null;
            }

            WorkflowArchivePayload? payload =
                WorkflowJson.Deserialize<WorkflowArchivePayload>(row.PayloadJson);
            if (payload == null)
            {
                // Die Zeile ist da, ihr Inhalt nicht lesbar - das ist ein Befund und nicht ein Detail,
                // das eben leer bleibt. Die Kopfdaten stehen in Spalten und werden trotzdem gezeigt.
                LogEnvironment.LogEvent(
                    $"Archiv: die Nutzlast des Vorgangs '{instanceId}' liess sich nicht lesen. Es werden "
                    + "nur die Kopfdaten gezeigt.", LogSeverity.Warning);
            }

            return new ArchivedInstanceDetail
            {
                Head = ToArchivedListItem(row),
                FaultMessage = row.FaultMessage,
                RootInstanceId = row.RootInstanceId,
                ParentInstanceId = row.ParentInstanceId,
                Variables = WorkflowJson.DeserializeVariables(payload?.VariablesJson)
                    .ToDictionary(v => v.Key, v => (object?)v.Value),
                History = payload?.History ?? new List<HistoryEntry>(),
                Comments = payload?.Comments ?? new List<WorkflowArchivedComment>(),
                Attachments = payload?.Attachments ?? new List<WorkflowArchivedAttachment>()
            };
        }

        /// <summary>Die Zeilen-Daten eines archivierten Vorgangs - ohne die Nutzlast anzufassen.</summary>
        private static ArchivedInstanceListItem ToArchivedListItem(WorkflowArchivedInstanceRow row)
            => new ArchivedInstanceListItem
            {
                InstanceId = row.InstanceId,
                DefinitionId = row.DefinitionId,
                DefinitionVersion = row.DefinitionVersion,
                DefinitionName = row.DefinitionName,
                Status = (WorkflowStatus)row.Status,
                CreatedUtc = row.CreatedUtc,
                EndedUtc = row.EndedUtc,
                ArchivedUtc = row.ArchivedUtc,
                FaultCode = row.FaultCode,
                HasParent = row.ParentInstanceId != null,
                AttachmentCount = row.AttachmentCount,
                AttachmentsPurged = row.AttachmentsPurgedUtc != null
            };

        /// <summary>Der Anzeigename der Definition eines Ausloesers, oder null.</summary>
        private static string? DefinitionName(WorkflowOperation op, WorkflowStartTrigger trigger)
        {
            try
            {
                // Ueber den Schluessel des Ausloesers - er zeigt auf GENAU die Fassung, aus der er stammt.
                return op.Store.GetDefinition(trigger.DefinitionKey)?.Name;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(
                    $"Konnte den Namen der Definition '{trigger.DefinitionId}' nicht lesen: "
                    + $"{ex.OutlineException()}", LogSeverity.Warning);
                return null;
            }
        }

        /// <summary>Feature-/Berechtigungs-Pruefung ueber die denormalisierten Namen am Ausloeser.</summary>
        private bool MayUseGate(string? requiredFeature, string? requiredPermission)
        {
            if (!string.IsNullOrWhiteSpace(requiredFeature)
                && !Services.VerifyActivatedFeatures(new[] { requiredFeature }, out _))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(requiredPermission)
                   || Services.VerifyUserPermissions(new[] { requiredPermission });
        }

        private bool MayStart(WorkflowDefinition definition)
            => (string.IsNullOrEmpty(definition.TenantId)
                || string.Equals(definition.TenantId, CurrentTenant(), StringComparison.OrdinalIgnoreCase))
               && MayUse(definition);

        /// <summary>
        /// Ob Mandant und Benutzer die Definition ueberhaupt <b>verwenden</b> duerfen: das verlangte
        /// Feature muss beim Mandanten aktiv und die verlangte Berechtigung beim Benutzer vorhanden sein.
        /// Beides leer = jeder darf.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Bewusst <b>ein</b> Praedikat fuer alle drei Wege - Liste, Start-Maske, Start. Getrennte
        /// Fassungen sind genau die, bei denen man den Knopf sieht und beim Klick abgewiesen wird.
        /// </para>
        /// <para>
        /// Das Feature wird hier UND bei jedem zeitgesteuerten Lauf geprueft (dort ueber
        /// <c>IWorkflowTenantFeatureGate</c>) - es haengt am Mandanten und gilt auch ohne Benutzer. Die
        /// Berechtigung kann nur hier geprueft werden: ein Zeitplan hat niemanden, den man fragen
        /// koennte.
        /// </para>
        /// </remarks>
        private bool MayUse(WorkflowDefinition definition)
            => MayUseGate(definition.RequiredFeature, definition.RequiredPermission);

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
                Suspended = row.Suspended,
                SuspendedReason = row.SuspendedReason,
                Priority = row.Priority,
                CorrelationKey = row.CorrelationKey,
                CreatedUtc = row.CreatedUtc,
                UpdatedUtc = row.UpdatedUtc
            };
        }

        /// <summary>Der Anmeldename des Benutzers - er steht im Verlauf, wenn jemand eingreift.</summary>
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
                case "Priority":
                    // Aufsteigend = die dringendsten zuerst (kleinere Zahl = wichtiger).
                    return descending ? q.OrderByDescending(r => r.Priority) : q.OrderBy(r => r.Priority);
                case "CreatedUtc":
                    return descending ? q.OrderByDescending(r => r.CreatedUtc) : q.OrderBy(r => r.CreatedUtc);
                default:
                    // Standard: zuletzt geaenderte zuerst.
                    return q.OrderByDescending(r => r.UpdatedUtc);
            }
        }
    }
}
