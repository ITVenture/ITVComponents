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
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options;
using ITVComponents.Workflow.EntityFramework;
using ITVComponents.Workflow.EntityFramework.Abstractions;
using ITVComponents.Workflow.Instances;
using ITVComponents.Workflow.Runtime;
using Microsoft.Extensions.Options;
using ITVComponents.Workflow.WebWorker;
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
            UserTaskListQuery query, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return new PagedResult<UserTaskListItem>();
            }

            using WorkflowOperation op = BeginOperation(environment);
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
                    RequiredPermission = x.Token.TaskPermission,
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
        public async Task<IReadOnlyList<string>> ListTaskKeysAsync(ClaimsPrincipal user, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return Array.Empty<string>();
            }

            using WorkflowOperation op = BeginOperation(environment);
            IQueryable<TokenRow> tokens = OpenTasks(op.LeaseContext());
            IReadOnlyCollection<string> allowed = await AllowedPermissionsAsync(tokens);
            return await RestrictToVisible(tokens, allowed)
                .Select(t => t.TaskKey!)
                .Distinct()
                .OrderBy(k => k)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<UserTaskListItem?> FindNextAsync(ClaimsPrincipal user, string instanceId,
            string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks) || string.IsNullOrWhiteSpace(instanceId))
            {
                return null;
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();

            // Dieselben Bausteine wie die Arbeitsliste - insbesondere OpenTasks, das schon auf den Tenant und
            // auf "wartend MIT Aufgabenart" einschraenkt. Das ist die verlaessliche Abgrenzung: der Abschluss
            // raeumt die Aufgabenart des Tokens ab und setzt es aktiv, ein weiterlaufender Zweig kann hier
            // also nicht als Aufgabe erscheinen. Die Token-Id bleibt dabei dieselbe - ein Zyklus, der spaeter
            // WIEDER an derselben Aufgabe haelt, ist deshalb ein voellig regulaerer naechster Schritt und
            // darf nicht ueber die Id ausgeschlossen werden.
            IQueryable<TokenRow> tokens = OpenTasks(ctx).Where(t => t.InstanceId == instanceId);
            IReadOnlyCollection<string> allowed = await AllowedPermissionsAsync(tokens);

            string? me = UserName(user);
            IQueryable<TokenRow> visible = RestrictToVisible(tokens, allowed)
                .Where(t => t.AssignedTo == me || t.AssignedTo == null);

            var joined = from t in visible
                         join i in ctx.WorkflowInstances.AsNoTracking() on t.InstanceId equals i.Id
                         select new UserTaskListItem
                         {
                             InstanceId = t.InstanceId,
                             TokenId = t.TokenId,
                             DefinitionId = i.DefinitionId,
                             NodeId = t.NodeId,
                             TaskKey = t.TaskKey!,
                             Title = t.TaskTitle,
                             AssignedTo = t.AssignedTo,
                             RequiredPermission = t.TaskPermission,
                             CreatedUtc = t.TaskCreatedUtc,
                             DueUtc = t.TaskDueUtc,
                             ClaimedBy = t.ClaimedBy,
                             ClaimedUntil = t.ClaimedUntil,
                             CorrelationKey = i.CorrelationKey
                         };

            // Aelteste zuerst, die Token-Id als zweites Kriterium: ohne sie waere die Reihenfolge zweier am
            // selben Zeitpunkt geparkter Aufgaben (ein paralleler Split) dem Zufall der Datenbank
            // ueberlassen - und damit auch, welche der Assistent als naechste zeigt.
            return await joined
                .OrderBy(t => t.CreatedUtc)
                .ThenBy(t => t.TokenId)
                .FirstOrDefaultAsync();
        }

        /// <inheritdoc/>
        public async Task<UserTaskDescriptor?> GetTaskAsync(ClaimsPrincipal user, string instanceId,
            string tokenId, string? environment = null)
        {
            if (!await MayWorkOnAsync(user, instanceId, tokenId, environment))
            {
                return null;
            }

            using WorkflowOperation op = BeginOperation(environment);
            return op.Engine.DescribeUserTask(instanceId, tokenId);
        }

        /// <inheritdoc/>
        public async Task<string?> ClaimAsync(ClaimsPrincipal user, string instanceId, string tokenId,
            TimeSpan duration, string? environment = null)
        {
            if (!await MayWorkOnAsync(user, instanceId, tokenId, environment))
            {
                return null;
            }

            string? me = UserName(user);
            using WorkflowOperation op = BeginOperation(environment);
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
        public async Task ReleaseClaimAsync(ClaimsPrincipal user, string instanceId, string tokenId,
            string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return;
            }

            string? me = UserName(user);
            using WorkflowOperation op = BeginOperation(environment);
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
        public async Task<UserTaskAssignmentStatus> ReassignAsync(ClaimsPrincipal user, string instanceId,
            string tokenId, string? newAssignee, string? reason = null, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                LogEnvironment.LogEvent(
                    $"Reassign denied: '{UserName(user)}' has no '{WorkflowSecurity.Tasks}' permission.",
                    LogSeverity.Warning);
                return UserTaskAssignmentStatus.NotFound;
            }

            string? me = UserName(user);
            using WorkflowOperation op = BeginOperation(environment);

            // Erst die Aufgabe im EIGENEN Mandanten finden: ohne sie ist jede weitere Frage gegenstandslos,
            // und der Weg ueber OpenTasks ist zugleich die Tenant-Grenze.
            var found = await OpenTasks(op.LeaseContext())
                .Where(t => t.InstanceId == instanceId && t.TokenId == tokenId)
                .Select(t => new { t.TaskPermission, t.AssignedTo })
                .FirstOrDefaultAsync();
            if (found == null)
            {
                LogEnvironment.LogEvent(
                    $"Reassign: task '{instanceId}/{tokenId}' is not an open task of the current tenant.",
                    LogSeverity.Report);
                return UserTaskAssignmentStatus.NotFound;
            }

            if (!MayReassign(user, me, found.AssignedTo, found.TaskPermission))
            {
                // Wie beim Abschluss: fuer den Aufrufer nicht von "gibt es nicht" unterscheidbar, im Log
                // aber sehr wohl - sonst sucht man den Grund in der Aufgabe statt in der Berechtigung.
                LogEnvironment.LogEvent(
                    $"Reassign denied: '{me}' may not reassign task '{instanceId}/{tokenId}' "
                    + $"(currently assigned to '{found.AssignedTo ?? "(pool)"}').", LogSeverity.Warning);
                return UserTaskAssignmentStatus.NotFound;
            }

            string? target = string.IsNullOrWhiteSpace(newAssignee) ? null : newAssignee.Trim();
            UserTaskAssignmentStatus status = op.Engine.ReassignUserTask(instanceId, tokenId, target, me, reason);
            if (status == UserTaskAssignmentStatus.Reassigned)
            {
                await ReleaseForeignClaimAsync(op, instanceId, tokenId, target);
            }

            return status;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<WorkflowComment>> ListCommentsAsync(ClaimsPrincipal user,
            string instanceId, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks) || string.IsNullOrWhiteSpace(instanceId))
            {
                return Array.Empty<WorkflowComment>();
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            string? tenant = CurrentTenant();
            return await ctx.WorkflowComments.AsNoTracking()
                .Where(c => c.InstanceId == instanceId && c.TenantId == tenant)
                .OrderBy(c => c.CreatedUtc)
                .ThenBy(c => c.CommentKey)
                .Select(c => new WorkflowComment
                {
                    CommentKey = c.CommentKey,
                    InstanceId = c.InstanceId,
                    TokenId = c.TokenId,
                    Author = c.Author,
                    CreatedUtc = c.CreatedUtc,
                    Text = c.Text
                })
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<WorkflowComment?> AddCommentAsync(ClaimsPrincipal user, string instanceId,
            string? tokenId, string text, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                LogEnvironment.LogEvent(
                    $"Comment denied: '{UserName(user)}' has no '{WorkflowSecurity.Tasks}' permission.",
                    LogSeverity.Warning);
                return null;
            }

            if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(text))
            {
                // Ein leerer Kommentar ist kein Fehler, aber auch nichts, was gespeichert gehoert - er
                // stuende als leere Zeile im Faden und liesse jeden raten, was gemeint war.
                return null;
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            string? tenant = CurrentTenant();

            // Den Vorgang im EIGENEN Mandanten nachweisen, bevor geschrieben wird - sonst genuegte eine
            // erratene Instanz-Id, um in einem fremden Vorgang zu schreiben.
            bool exists = await ctx.WorkflowInstances.AsNoTracking()
                .AnyAsync(i => i.Id == instanceId && i.TenantId == tenant);
            if (!exists)
            {
                LogEnvironment.LogEvent(
                    $"Comment denied: instance '{instanceId}' does not exist in the current tenant.",
                    LogSeverity.Warning);
                return null;
            }

            var row = new WorkflowCommentRow
            {
                InstanceId = instanceId,
                TokenId = string.IsNullOrWhiteSpace(tokenId) ? null : tokenId,
                TenantId = tenant,
                Author = UserName(user),
                CreatedUtc = DateTime.UtcNow,
                Text = text.Trim()
            };
            ctx.WorkflowComments.Add(row);
            await ctx.SaveChangesAsync();

            return new WorkflowComment
            {
                CommentKey = row.CommentKey,
                InstanceId = row.InstanceId,
                TokenId = row.TokenId,
                Author = row.Author,
                CreatedUtc = row.CreatedUtc,
                Text = row.Text
            };
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<WorkflowAttachment>> ListAttachmentsAsync(ClaimsPrincipal user,
            string instanceId, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks) || string.IsNullOrWhiteSpace(instanceId))
            {
                return Array.Empty<WorkflowAttachment>();
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            string? tenant = CurrentTenant();
            // Nur die Beschreibung - der Inhalt liegt in einer anderen Tabelle und wird hier nicht
            // angefasst. Genau dafuer sind die beiden getrennt.
            return await ctx.WorkflowAttachments.AsNoTracking()
                .Where(a => a.InstanceId == instanceId && a.TenantId == tenant)
                .OrderBy(a => a.CreatedUtc)
                .ThenBy(a => a.AttachmentKey)
                .Select(a => new WorkflowAttachment
                {
                    AttachmentKey = a.AttachmentKey,
                    InstanceId = a.InstanceId,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    SizeBytes = a.SizeBytes,
                    Author = a.Author,
                    CreatedUtc = a.CreatedUtc
                })
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<WorkflowAttachment?> AddAttachmentAsync(ClaimsPrincipal user, string instanceId,
            string? tokenId, string fileName, string? contentType, byte[] content,
            string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                LogEnvironment.LogEvent(
                    $"Attachment denied: '{UserName(user)}' has no '{WorkflowSecurity.Tasks}' permission.",
                    LogSeverity.Warning);
                return null;
            }

            if (string.IsNullOrWhiteSpace(instanceId) || content == null || content.Length == 0
                || string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            long limit = MaxAttachmentBytes;
            if (limit <= 0 || content.LongLength > limit)
            {
                // Die Grenze ist eine Aussage der Anlage, kein technischer Zufall - deshalb mit Zahl im
                // Log, damit man sie wiederfindet, wenn ein Benutzer sich beschwert.
                LogEnvironment.LogEvent(
                    $"Attachment '{fileName}' ({content.LongLength} bytes) was rejected: the limit is "
                    + $"{limit} bytes ({(limit <= 0 ? "attachments are switched off" : "configured")}).",
                    LogSeverity.Warning);
                return null;
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            string? tenant = CurrentTenant();

            bool exists = await ctx.WorkflowInstances.AsNoTracking()
                .AnyAsync(i => i.Id == instanceId && i.TenantId == tenant);
            if (!exists)
            {
                LogEnvironment.LogEvent(
                    $"Attachment denied: instance '{instanceId}' does not exist in the current tenant.",
                    LogSeverity.Warning);
                return null;
            }

            // ERST den Inhalt ablegen, DANN die Beschreibung schreiben: andersherum entstuende bei einem
            // Fehler eine Beschreibung ohne Datei - ein Anhang, den man sieht und nicht oeffnen kann.
            // Umgekehrt bleibt schlimmstenfalls ein Inhalt liegen, den niemand sieht.
            string identifier = await AttachmentStore(op)
                .SaveAsync(content, contentType, fileName);

            var row = new WorkflowAttachmentRow
            {
                InstanceId = instanceId,
                TokenId = string.IsNullOrWhiteSpace(tokenId) ? null : tokenId,
                TenantId = tenant,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = content.LongLength,
                Author = UserName(user),
                CreatedUtc = DateTime.UtcNow,
                FileIdentifier = identifier
            };
            ctx.WorkflowAttachments.Add(row);
            await ctx.SaveChangesAsync();

            return new WorkflowAttachment
            {
                AttachmentKey = row.AttachmentKey,
                InstanceId = row.InstanceId,
                FileName = row.FileName,
                ContentType = row.ContentType,
                SizeBytes = row.SizeBytes,
                Author = row.Author,
                CreatedUtc = row.CreatedUtc
            };
        }

        /// <inheritdoc/>
        public async Task<WorkflowAttachmentDownload?> OpenAttachmentAsync(ClaimsPrincipal user,
            string instanceId, int attachmentKey, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return null;
            }

            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            string? tenant = CurrentTenant();

            // Instanz UND Mandant muessen passen - eine erratene Schluesselzahl darf keine fremde Datei
            // herausgeben.
            var found = await ctx.WorkflowAttachments.AsNoTracking()
                .Where(a => a.AttachmentKey == attachmentKey && a.InstanceId == instanceId
                            && a.TenantId == tenant)
                .Select(a => new { a.FileIdentifier, a.FileName, a.ContentType })
                .FirstOrDefaultAsync();
            if (found == null)
            {
                LogEnvironment.LogEvent(
                    $"Attachment {attachmentKey} of instance '{instanceId}' was not found in the current "
                    + "tenant.", LogSeverity.Report);
                return null;
            }

            WorkflowAttachmentContent? content = await AttachmentStore(op).OpenAsync(found.FileIdentifier);
            if (content == null)
            {
                return null;
            }

            return new WorkflowAttachmentDownload
            {
                Content = content.Content,
                FileName = found.FileName ?? content.DownloadName ?? "download",
                ContentType = found.ContentType ?? content.ContentType
            };
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAttachmentAsync(ClaimsPrincipal user, string instanceId,
            int attachmentKey, string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return false;
            }

            string? me = UserName(user);
            using WorkflowOperation op = BeginOperation(environment);
            WorkflowContext ctx = op.LeaseContext();
            string? tenant = CurrentTenant();

            WorkflowAttachmentRow? row = await ctx.WorkflowAttachments
                .FirstOrDefaultAsync(a => a.AttachmentKey == attachmentKey && a.InstanceId == instanceId
                                          && a.TenantId == tenant);
            if (row == null)
            {
                return false;
            }

            if (row.Author != me)
            {
                // Den eigenen Fehlgriff zu korrigieren ist etwas anderes, als fremde Belege aus einem
                // Vorgang zu entfernen - dafuer gibt es hier bewusst keinen Weg.
                LogEnvironment.LogEvent(
                    $"Delete of attachment {attachmentKey} denied: '{me}' is not its author "
                    + $"('{row.Author}').", LogSeverity.Warning);
                return false;
            }

            string identifier = row.FileIdentifier;
            ctx.WorkflowAttachments.Remove(row);
            await ctx.SaveChangesAsync();

            // Der Inhalt danach - er ist ab jetzt unerreichbar, und ein Fehler beim Aufraeumen soll das
            // Entfernen nicht zurueckdrehen (der Store protokolliert ihn).
            await AttachmentStore(op).DeleteAsync(identifier);
            return true;
        }

        /// <summary>
        /// Die Ablage fuer Anhaenge: die vom Host registrierte, sonst die eingebaute (Datenbank).
        /// </summary>
        /// <remarks>
        /// Der Rueckfall ist Absicht - Anhaenge sollen ohne Einrichtung funktionieren. Wer sie woanders
        /// haben will, registriert eine eigene Umsetzung, und hier aendert sich nichts.
        /// </remarks>
        private IWorkflowAttachmentStore AttachmentStore(WorkflowOperation op)
            => services.GetService<IWorkflowAttachmentStore>()
               ?? new EfWorkflowAttachmentStore(op.LeaseContext);

        /// <summary>Die konfigurierte Obergrenze fuer einen Anhang (Standard 10 MB).</summary>
        private long MaxAttachmentBytes
            => services.GetService<IOptions<WorkflowViewsOptions>>()?.Value?.MaxAttachmentBytes
               ?? 10 * 1024 * 1024;

        /// <summary>
        /// Der Mandant des laufenden Kontexts - dieselbe Quelle wie in <see cref="OpenTasks"/>, damit
        /// Arbeitsliste und Kommentare nicht unterschiedlich abgrenzen, und ueber
        /// <see cref="WorkflowTenant.Normalize"/> dieselbe Schreibweise wie
        /// <c>WorkflowContext.CurrentTenant</c>.
        /// </summary>
        private string? CurrentTenant()
            => WorkflowTenant.Normalize(services.GetService<IPermissionScope>()?.PermissionPrefix);

        /// <summary>
        /// Darf dieser Benutzer diese Aufgabe umtragen? Zwei Wege: die Vertretungs-Berechtigung
        /// (<see cref="WorkflowSecurity.AssignTasks"/>) oder die eigene Handreichung.
        /// </summary>
        /// <param name="user">der Benutzer</param>
        /// <param name="me">sein Benutzername</param>
        /// <param name="assignedTo">der aktuelle Zustaendige der Aufgabe (null = Pool)</param>
        /// <param name="taskPermission">die Permission des Aufgaben-Knotens (null/leer = keine)</param>
        /// <returns>true, wenn er umtragen darf</returns>
        /// <remarks>
        /// <para>
        /// Der Vertretungs-Weg verlangt bewusst NICHT die fachliche Permission des Knotens: wer die
        /// Aufgaben eines Erkrankten verteilt, muss sie nicht selbst erledigen duerfen. Er sieht dabei auch
        /// nichts Fachliches - das Oeffnen der Maske haengt unveraendert an
        /// <see cref="MayWorkOnAsync"/>.
        /// </para>
        /// <para>
        /// Der eigene Weg deckt Abgeben, Zuruecklegen und Ansichnehmen ab. Er verlangt umgekehrt sehr wohl
        /// die Knoten-Permission, denn er stuetzt sich genau darauf: wer die Aufgabe ohnehin erledigen
        /// duerfte, darf sie auch weiterreichen. Eine Aufgabe, die bereits einem ANDEREN gehoert, ist
        /// damit tabu - sie einem Dritten wegzunehmen ist eine organisatorische Handlung und braucht die
        /// Berechtigung dafuer.
        /// </para>
        /// </remarks>
        private bool MayReassign(ClaimsPrincipal user, string? me, string? assignedTo, string? taskPermission)
        {
            if (HasPermission(user, WorkflowSecurity.AssignTasks))
            {
                return true;
            }

            bool mayWorkOn = string.IsNullOrWhiteSpace(taskPermission)
                             || services.VerifyUserPermissions(new[] { taskPermission });
            bool mineOrPool = assignedTo == null || assignedTo == me;
            return mayWorkOn && mineOrPool;
        }

        /// <summary>
        /// Hebt die weiche Sperre auf, wenn sie nach dem Umtragen nicht mehr zum Zustaendigen passt.
        /// </summary>
        /// <param name="op">die laufende Operation</param>
        /// <param name="instanceId">die Instanz</param>
        /// <param name="tokenId">die Aufgabe</param>
        /// <param name="newAssignee">der neue Zustaendige (null = Pool)</param>
        /// <remarks>
        /// Die Sperre sagt "wird gerade bearbeitet". Nach einer Uebergabe stimmt das nicht mehr - die Liste
        /// zeigte sonst den Vorgaenger als Bearbeiter einer Aufgabe, die ihm gar nicht mehr gehoert, und
        /// der neue Zustaendige liesse sie aus Ruecksicht liegen. Bleibt der Zustaendige derselbe wie der
        /// Inhaber der Sperre (jemand nimmt eine Pool-Aufgabe an sich, die er schon offen hat), bleibt sie
        /// stehen.
        /// <para>
        /// Ein Direktschreiben auf die Zeile ist hier - anders als beim Zustaendigen - richtig: die weiche
        /// Sperre gibt es NUR auf der Zeile, die Engine kennt sie nicht, und kein Instanz-Commit
        /// ueberschreibt sie.
        /// </para>
        /// </remarks>
        private static async Task ReleaseForeignClaimAsync(WorkflowOperation op, string instanceId,
            string tokenId, string? newAssignee)
        {
            WorkflowContext ctx = op.LeaseContext();
            TokenRow? row = await ctx.Tokens
                .FirstOrDefaultAsync(t => t.InstanceId == instanceId && t.TokenId == tokenId);
            if (row == null || row.ClaimedBy == null || row.ClaimedBy == newAssignee)
            {
                return;
            }

            row.ClaimedBy = null;
            row.ClaimedUntil = null;
            await ctx.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<UserTaskCompletionResult> CompleteAsync(ClaimsPrincipal user, string instanceId,
            string tokenId, IDictionary<string, object>? result, string? environment = null)
        {
            if (!await MayWorkOnAsync(user, instanceId, tokenId, environment))
            {
                // Kein Recht auf GENAU diese Aufgabe - fuer den Aufrufer nicht von "gibt es nicht"
                // unterscheidbar (und das ist beabsichtigt), im Log aber sehr wohl.
                LogEnvironment.LogEvent(
                    $"CompleteUserTask denied: '{UserName(user)}' may not work on task '{instanceId}/{tokenId}'.",
                    LogSeverity.Warning);
                return new UserTaskCompletionResult(UserTaskCompletionStatus.NotFound, Array.Empty<string>());
            }

            using WorkflowOperation op = BeginOperation(environment);
            UserTaskCompletionResult completion = op.Engine.CompleteUserTask(instanceId, tokenId, result,
                UserName(user));
            if (completion.Success)
            {
                await AdvanceAsync(op, instanceId, completion.ActivatedTokenIds);
                // Best-effort Wake des (evtl. im selben Prozess laufenden) Workers fuer diesen Tenant/diese
                // Umgebung. Fehlt der Worker, ist der Service nicht registriert -> stiller No-op.
                services.GetService<IWorkflowWorkerWake>()?.Poke(environment, op.Store.GetInstance(instanceId)?.TenantId);
            }

            return completion;
        }

        /// <summary>
        /// Laesst den durch den Abschluss aktiv gewordenen Zweig weiterlaufen. Die Variante bestimmt, ob
        /// das inline im Web-Prozess passiert oder ein Runner ihn aufnimmt.
        /// </summary>
        protected abstract Task AdvanceAsync(WorkflowOperation op, string instanceId,
            IReadOnlyList<string> tokenIds);

        /// <summary>
        /// Oeffnet eine neue Operation (frischer Kontext, Engine ueber die Host-Factory). Der Store richtet
        /// sich nach der (optional) gewaehlten Umgebung; ohne Umgebung/Settings der Standard-Store.
        /// </summary>
        protected WorkflowOperation BeginOperation(string? environment = null)
            => new WorkflowOperation(freshContext, services.GetService<WorkflowEngineFactory>(),
                WorkflowEnvironmentResolver.StoreDependencyName(services, environment));

        /// <summary>
        /// Alle offenen Aufgaben des aktuellen Tenants. Der Tenant wird <b>explizit</b> gefiltert und nicht
        /// dem globalen Query-Filter ueberlassen: <c>TokenRow</c> hat keinen, und ob der Instanz-Filter
        /// ueberhaupt greift, entscheidet die Registrierung des Kontexts im Host (der Weg ueber die
        /// DbContext-Factory ist bewusst filterfrei). Eine Arbeitsliste darf davon nicht abhaengen.
        /// </summary>
        private IQueryable<TokenRow> OpenTasks(WorkflowContext ctx)
        {
            string? tenant = CurrentTenant();
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
                .Where(t => t.TaskPermission != null && t.TaskPermission != "")
                .Select(t => t.TaskPermission!)
                .Distinct()
                .ToListAsync();

            return required.Where(p => services.VerifyUserPermissions(new[] { p })).ToList();
        }

        /// <summary>
        /// Schraenkt auf die Aufgaben ein, die der Benutzer sehen darf. "Keine Permission noetig" ist
        /// null ODER leer: der Knoten-Editor schreibt fuer ein geleertes Feld einen Leerstring, und eine
        /// leere Permission hat niemand - die Aufgabe waere sonst fuer JEDEN unsichtbar.
        /// </summary>
        private static IQueryable<TokenRow> RestrictToVisible(IQueryable<TokenRow> tasks,
            IReadOnlyCollection<string> allowed)
        {
            var allowedList = allowed.ToList();
            return tasks.Where(t => t.TaskPermission == null || t.TaskPermission == ""
                                    || allowedList.Contains(t.TaskPermission));
        }

        /// <summary>
        /// Darf dieser Benutzer an GENAU dieser Aufgabe arbeiten? Prueft das allgemeine Aufgaben-Recht, die
        /// Existenz der offenen Aufgabe im eigenen Tenant und die Permission des Knotens. Ohne diese
        /// Pruefung genuegte das Erraten einer Token-Id.
        /// </summary>
        private async Task<bool> MayWorkOnAsync(ClaimsPrincipal user, string instanceId, string tokenId,
            string? environment = null)
        {
            if (!HasPermission(user, WorkflowSecurity.Tasks))
            {
                return false;
            }

            using WorkflowOperation op = BeginOperation(environment);
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

            return string.IsNullOrWhiteSpace(found.TaskPermission)
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
