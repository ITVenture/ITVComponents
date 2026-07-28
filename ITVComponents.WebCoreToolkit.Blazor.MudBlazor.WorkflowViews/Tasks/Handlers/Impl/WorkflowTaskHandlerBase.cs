using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Common;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Runtime;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.ViewModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.Workflow;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.Instances;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Tasks.Handlers.Impl
{
    /// <summary>
    /// Gemeinsame Basis der Aufgaben-Handler. Wie im Monitoring laeuft jede Operation ueber eine
    /// <see cref="WorkflowOperation"/> mit frischem <c>WorkflowContext</c>; der EINZIGE Unterschied
    /// zwischen den Varianten ist, ob der abgeschlossene Zweig <b>inline</b> weiterlaeuft oder ein Runner
    /// ihn aufnimmt.
    /// </summary>
    internal abstract class WorkflowTaskHandlerBase : IWorkflowTaskHandler
    {
        private readonly IServiceProvider services;
        private readonly IFreshInjectablePlugin<WorkflowContext> freshContext;

        protected WorkflowTaskHandlerBase(IServiceProvider services,
            IFreshInjectablePlugin<WorkflowContext> freshContext)
        {
            this.services = services;
            this.freshContext = freshContext;
        }

        /// <inheritdoc/>
        public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
            => services.VerifyUserPermissions(permissions);

        /// <inheritdoc/>
        public async Task<PagedResult<UserTaskListItem>> ListTasksAsync(ClaimsPrincipal user,
            UserTaskListQuery query)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return new PagedResult<UserTaskListItem>();
            }

            using WorkflowOperation op = BeginOperation();
            WorkflowContext ctx = op.LeaseContext();

            IQueryable<TokenRow> tokens = OpenTasks(ctx);
            IReadOnlyCollection<string> allowed = await AllowedPermissionsAsync(tokens);
            IQueryable<TokenRow> visible = RestrictToVisible(tokens, allowed);

            string? me = UserName(user);
            switch (query.Scope)
            {
                case UserTaskScope.Mine:
                    visible = visible.Where(t => t.AssignedTo == me);
                    break;
                case UserTaskScope.Pool:
                    visible = visible.Where(t => t.AssignedTo == null);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(query.TaskKey))
            {
                string key = query.TaskKey!;
                visible = visible.Where(t => t.TaskKey == key);
            }

            if (query.OverdueOnly)
            {
                DateTime now = DateTime.UtcNow;
                visible = visible.Where(t => t.TaskDueUtc != null && t.TaskDueUtc < now);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string term = query.Search!;
                visible = visible.Where(t => (t.TaskTitle != null && t.TaskTitle.Contains(term))
                                             || t.TaskKey!.Contains(term)
                                             || t.InstanceId.Contains(term));
            }

            var joined = from t in visible
                         join i in ctx.WorkflowInstances.AsNoTracking() on t.InstanceId equals i.Id
                         select new { Token = t, Instance = i };

            int total = await joined.CountAsync();
            var page = await Sort(joined.Select(x => new UserTaskListItem
                {
                    InstanceId = x.Token.InstanceId,
                    TokenId = x.Token.TokenId,
                    DefinitionId = x.Instance.DefinitionId,
                    NodeId = x.Token.NodeId,
                    TaskKey = x.Token.TaskKey!,
                    Title = x.Token.TaskTitle,
                    AssignedTo = x.Token.AssignedTo,
                    CreatedUtc = x.Token.TaskCreatedUtc,
                    DueUtc = x.Token.TaskDueUtc,
                    ClaimedBy = x.Token.ClaimedBy,
                    ClaimedUntil = x.Token.ClaimedUntil,
                    CorrelationKey = x.Instance.CorrelationKey
                }), query.SortColumn, query.SortDescending)
                .Skip(query.Page * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new PagedResult<UserTaskListItem> { Items = page, TotalCount = total };
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> ListTaskKeysAsync(ClaimsPrincipal user)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return Array.Empty<string>();
            }

            using WorkflowOperation op = BeginOperation();
            IQueryable<TokenRow> tokens = OpenTasks(op.LeaseContext());
            IReadOnlyCollection<string> allowed = await AllowedPermissionsAsync(tokens);
            return await RestrictToVisible(tokens, allowed)
                .Select(t => t.TaskKey!)
                .Distinct()
                .OrderBy(k => k)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<UserTaskDescriptor?> GetTaskAsync(ClaimsPrincipal user, string instanceId,
            string tokenId)
        {
            if (!await MayWorkOnAsync(user, instanceId, tokenId))
            {
                return null;
            }

            using WorkflowOperation op = BeginOperation();
            return op.Engine.DescribeUserTask(instanceId, tokenId);
        }

        /// <inheritdoc/>
        public async Task<string?> ClaimAsync(ClaimsPrincipal user, string instanceId, string tokenId,
            TimeSpan duration)
        {
            if (!await MayWorkOnAsync(user, instanceId, tokenId))
            {
                return null;
            }

            string? me = UserName(user);
            using WorkflowOperation op = BeginOperation();
            WorkflowContext ctx = op.LeaseContext();
            TokenRow? row = await ctx.Tokens
                .FirstOrDefaultAsync(t => t.InstanceId == instanceId && t.TokenId == tokenId);
            if (row == null)
            {
                return null;
            }

            DateTime now = DateTime.UtcNow;
            string? foreignOwner = row.ClaimedBy != null && row.ClaimedBy != me
                                   && row.ClaimedUntil != null && row.ClaimedUntil > now
                ? row.ClaimedBy
                : null;

            // Auch bei fremdem Claim wird uebernommen: die Sperre ist weich. Der Aufrufer bekommt den
            // bisherigen Inhaber zurueck und sagt es dem Benutzer - er entscheidet, ob er trotzdem
            // weitermacht.
            row.ClaimedBy = me;
            row.ClaimedUntil = now.Add(duration <= TimeSpan.Zero ? TimeSpan.FromMinutes(15) : duration);
            await ctx.SaveChangesAsync();
            return foreignOwner;
        }

        /// <inheritdoc/>
        public async Task ReleaseClaimAsync(ClaimsPrincipal user, string instanceId, string tokenId)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return;
            }

            string? me = UserName(user);
            using WorkflowOperation op = BeginOperation();
            WorkflowContext ctx = op.LeaseContext();
            TokenRow? row = await ctx.Tokens
                .FirstOrDefaultAsync(t => t.InstanceId == instanceId && t.TokenId == tokenId);
            if (row == null || row.ClaimedBy != me)
            {
                // Nichts freizugeben (Aufgabe erledigt oder ein anderer hat die Sperre inzwischen
                // uebernommen) - kein Fehler, aber es soll nachvollziehbar bleiben.
                LogEnvironment.LogEvent(
                    $"ReleaseClaim: task '{instanceId}/{tokenId}' is not claimed by '{me}' - nothing released.",
                    LogSeverity.Report);
                return;
            }

            row.ClaimedBy = null;
            row.ClaimedUntil = null;
            await ctx.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<UserTaskCompletionResult> CompleteAsync(ClaimsPrincipal user, string instanceId,
            string tokenId, IDictionary<string, object>? result)
        {
            if (!await MayWorkOnAsync(user, instanceId, tokenId))
            {
                // Kein Recht auf GENAU diese Aufgabe - fuer den Aufrufer nicht von "gibt es nicht"
                // unterscheidbar (und das ist beabsichtigt), im Log aber sehr wohl.
                LogEnvironment.LogEvent(
                    $"CompleteUserTask denied: '{UserName(user)}' may not work on task '{instanceId}/{tokenId}'.",
                    LogSeverity.Warning);
                return new UserTaskCompletionResult(UserTaskCompletionStatus.NotFound, Array.Empty<string>());
            }

            using WorkflowOperation op = BeginOperation();
            UserTaskCompletionResult completion = op.Engine.CompleteUserTask(instanceId, tokenId, result,
                UserName(user));
            if (completion.Success)
            {
                await AdvanceAsync(op, instanceId, completion.ActivatedTokenIds);
            }

            return completion;
        }

        /// <summary>
        /// Laesst den durch den Abschluss aktiv gewordenen Zweig weiterlaufen. Die Variante bestimmt, ob
        /// das inline im Web-Prozess passiert oder ein Runner ihn aufnimmt.
        /// </summary>
        protected abstract Task AdvanceAsync(WorkflowOperation op, string instanceId,
            IReadOnlyList<string> tokenIds);

        /// <summary>Oeffnet eine neue Operation (frischer Kontext, Engine ueber die Host-Factory).</summary>
        protected WorkflowOperation BeginOperation()
            => new WorkflowOperation(freshContext, services.GetService<WorkflowEngineFactory>());

        /// <summary>
        /// Alle offenen Aufgaben des aktuellen Tenants. Der Tenant wird <b>explizit</b> gefiltert und nicht
        /// dem globalen Query-Filter ueberlassen: <c>TokenRow</c> hat keinen, und ob der Instanz-Filter
        /// ueberhaupt greift, entscheidet die Registrierung des Kontexts im Host (der Weg ueber die
        /// DbContext-Factory ist bewusst filterfrei). Eine Arbeitsliste darf davon nicht abhaengen.
        /// </summary>
        private IQueryable<TokenRow> OpenTasks(WorkflowContext ctx)
        {
            string? tenant = services.GetService<IPermissionScope>()?.PermissionPrefix?.ToLower();
            int waiting = (int)TokenStatus.Waiting;
            int running = (int)WorkflowStatus.Running;
            int instanceWaiting = (int)WorkflowStatus.Waiting;
            return ctx.Tokens.AsNoTracking()
                .Where(t => t.Status == waiting && t.TaskKey != null && t.TenantId == tenant
                            && ctx.WorkflowInstances.Any(i => i.Id == t.InstanceId
                                                             && (i.Status == running
                                                                 || i.Status == instanceWaiting)));
        }

        /// <summary>
        /// Die Permissions der offenen Aufgaben, die der aktuelle Benutzer tatsaechlich hat. Bewusst in
        /// zwei Schritten: welche Permission ein Benutzer hat, ist keine Abfrage, die in SQL laufen kann -
        /// die Menge der VORKOMMENDEN Aufgaben-Permissions ist dagegen klein und stabil.
        /// </summary>
        private async Task<IReadOnlyCollection<string>> AllowedPermissionsAsync(IQueryable<TokenRow> tasks)
        {
            List<string> required = await tasks
                .Where(t => t.TaskPermission != null)
                .Select(t => t.TaskPermission!)
                .Distinct()
                .ToListAsync();

            return required.Where(p => services.VerifyUserPermissions(new[] { p })).ToList();
        }

        /// <summary>Schraenkt auf die Aufgaben ein, die der Benutzer sehen darf.</summary>
        private static IQueryable<TokenRow> RestrictToVisible(IQueryable<TokenRow> tasks,
            IReadOnlyCollection<string> allowed)
        {
            var allowedList = allowed.ToList();
            return tasks.Where(t => t.TaskPermission == null || allowedList.Contains(t.TaskPermission));
        }

        /// <summary>
        /// Darf dieser Benutzer an GENAU dieser Aufgabe arbeiten? Prueft das allgemeine Aufgaben-Recht, die
        /// Existenz der offenen Aufgabe im eigenen Tenant und die Permission des Knotens. Ohne diese
        /// Pruefung genuegte das Erraten einer Token-Id.
        /// </summary>
        private async Task<bool> MayWorkOnAsync(ClaimsPrincipal user, string instanceId, string tokenId)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return false;
            }

            using WorkflowOperation op = BeginOperation();
            var found = await OpenTasks(op.LeaseContext())
                .Where(t => t.InstanceId == instanceId && t.TokenId == tokenId)
                .Select(t => new { t.TaskPermission })
                .FirstOrDefaultAsync();

            // Kein Treffer = es gibt die offene Aufgabe in diesem Tenant nicht (mehr). Die Unterscheidung
            // "erledigt" von "nie dagewesen" macht der Abschluss selbst.
            if (found == null)
            {
                return false;
            }

            return found.TaskPermission == null
                   || services.VerifyUserPermissions(new[] { found.TaskPermission });
        }

        private static string? UserName(ClaimsPrincipal user) => user?.Identity?.Name;

        private static IQueryable<UserTaskListItem> Sort(IQueryable<UserTaskListItem> q, string? column,
            bool descending)
        {
            switch (column)
            {
                case nameof(UserTaskListItem.Title):
                    return descending ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title);
                case nameof(UserTaskListItem.TaskKey):
                    return descending ? q.OrderByDescending(t => t.TaskKey) : q.OrderBy(t => t.TaskKey);
                case nameof(UserTaskListItem.AssignedTo):
                    return descending ? q.OrderByDescending(t => t.AssignedTo) : q.OrderBy(t => t.AssignedTo);
                case nameof(UserTaskListItem.DueUtc):
                    return descending ? q.OrderByDescending(t => t.DueUtc) : q.OrderBy(t => t.DueUtc);
                case nameof(UserTaskListItem.CreatedUtc):
                    return descending ? q.OrderByDescending(t => t.CreatedUtc) : q.OrderBy(t => t.CreatedUtc);
                default:
                    // Standard: aelteste zuerst - eine Arbeitsliste wird von unten abgearbeitet.
                    return q.OrderBy(t => t.CreatedUtc);
            }
        }
    }
}
