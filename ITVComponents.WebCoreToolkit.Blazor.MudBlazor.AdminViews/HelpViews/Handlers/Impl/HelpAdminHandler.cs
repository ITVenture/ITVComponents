using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers.Impl
{
    /// <summary>
    /// Default <see cref="IHelpAdminHandler"/> over a global help context. Uses a per-operation context from the
    /// factory (Blazor-safe) and gates every operation, authoritatively, by the <c>Help.Admin.Topics.*</c>
    /// permissions. Help topics are global (no tenant scope).
    /// </summary>
    public class HelpAdminHandler<TContext> : IHelpAdminHandler
        where TContext : DbContext, IHelpSystemContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IServiceProvider services;

        public HelpAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
        {
            this.dbFactory = dbFactory;
            this.services = services;
        }

        public bool CanManage(ClaimsPrincipal user) => services.VerifyUserPermissions(HelpPermissions.TopicsRead);

        public bool CanWrite(ClaimsPrincipal user) => services.VerifyUserPermissions(HelpPermissions.TopicsWriteAny);

        public async Task<HelpTopicNodeViewModel[]> ListChildrenAsync(ClaimsPrincipal admin, int? parentId, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.TopicsRead))
            {
                return Array.Empty<HelpTopicNodeViewModel>();
            }

            var culture = CultureInfo.CurrentUICulture.Name;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            IQueryable<HelpTopic> query = db.HelpTopics.AsNoTracking();
            query = parentId.HasValue ? query.Where(t => t.ParentId == parentId.Value) : query.Where(t => t.ParentId == null);

            var rows = await query
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Slug)
                .Select(t => new
                {
                    t.HelpTopicId,
                    t.ParentId,
                    t.Kind,
                    t.Slug,
                    t.Icon,
                    t.IsPublished,
                    t.ShowInMenu,
                    t.SortOrder,
                    ChildCount = db.HelpTopics.Count(c => c.ParentId == t.HelpTopicId),
                    Contents = t.Contents.Select(c => new CultureTitle { Culture = c.Culture, Title = c.Title }).ToList()
                })
                .ToListAsync(ct);

            return rows.Select(t => new HelpTopicNodeViewModel
            {
                HelpTopicId = t.HelpTopicId,
                ParentId = t.ParentId,
                Kind = t.Kind,
                Slug = t.Slug,
                Icon = t.Icon,
                IsPublished = t.IsPublished,
                ShowInMenu = t.ShowInMenu,
                SortOrder = t.SortOrder,
                ChildCount = t.ChildCount,
                Title = ResolveTitle(t.Contents, culture, t.Slug)
            }).ToArray();
        }

        public async Task<HelpTopicEditViewModel?> GetTopicAsync(ClaimsPrincipal admin, int helpTopicId, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.TopicsRead))
            {
                return null;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var topic = await db.HelpTopics.AsNoTracking().Include(t => t.Contents)
                .FirstOrDefaultAsync(t => t.HelpTopicId == helpTopicId, ct);
            if (topic == null)
            {
                return null;
            }

            return new HelpTopicEditViewModel
            {
                HelpTopicId = topic.HelpTopicId,
                ParentId = topic.ParentId,
                Kind = topic.Kind,
                Slug = topic.Slug,
                Icon = topic.Icon,
                IsPublished = topic.IsPublished,
                ShowInMenu = topic.ShowInMenu,
                SortOrder = topic.SortOrder,
                Contents = topic.Contents
                    .OrderBy(c => c.Culture)
                    .Select(c => new HelpTopicContentViewModel { Culture = c.Culture, Title = c.Title, Body = c.Body })
                    .ToList()
            };
        }

        public async Task<int?> SaveTopicAsync(ClaimsPrincipal admin, HelpTopicEditViewModel model, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.TopicsWriteAny) || string.IsNullOrWhiteSpace(model.Slug))
            {
                return null;
            }

            var slug = model.Slug.Trim();
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Slug is the stable, unique deep-link key.
            if (await db.HelpTopics.AnyAsync(t => t.Slug == slug && t.HelpTopicId != model.HelpTopicId, ct))
            {
                return null;
            }

            HelpTopic topic;
            if (model.HelpTopicId != 0)
            {
                topic = await db.HelpTopics.Include(t => t.Contents)
                    .FirstOrDefaultAsync(t => t.HelpTopicId == model.HelpTopicId, ct);
                if (topic == null)
                {
                    return null;
                }
            }
            else
            {
                topic = new HelpTopic();
                db.HelpTopics.Add(topic);
            }

            topic.ParentId = model.ParentId;
            topic.Kind = model.Kind;
            topic.Slug = slug;
            topic.Icon = model.Icon;
            topic.IsPublished = model.IsPublished;
            topic.ShowInMenu = model.ShowInMenu;
            topic.SortOrder = model.SortOrder;

            ReconcileContents(db, topic, model);

            await db.SaveChangesAsync(ct);
            return topic.HelpTopicId;
        }

        public async Task<bool> DeleteTopicAsync(ClaimsPrincipal admin, int helpTopicId, CancellationToken ct = default)
        {
            if (!services.VerifyUserPermissions(HelpPermissions.TopicsWriteAny))
            {
                return false;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var topic = await db.HelpTopics.FirstOrDefaultAsync(t => t.HelpTopicId == helpTopicId, ct);
            if (topic == null)
            {
                return false;
            }

            // A parent may not be deleted while it still holds children (self-ref is Restrict); the caller must
            // empty or move the subtree first.
            if (await db.HelpTopics.AnyAsync(c => c.ParentId == helpTopicId, ct))
            {
                return false;
            }

            db.HelpTopics.Remove(topic); // localized contents cascade
            await db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>Upserts the localized content rows: updates/inserts the incoming cultures, removes the rest.</summary>
        private static void ReconcileContents(TContext db, HelpTopic topic, HelpTopicEditViewModel model)
        {
            var incoming = model.Contents
                .Where(c => !string.IsNullOrWhiteSpace(c.Culture) && !string.IsNullOrWhiteSpace(c.Title))
                .GroupBy(c => c.Culture.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var incomingCultures = new HashSet<string>(incoming.Select(c => c.Culture.Trim()), StringComparer.OrdinalIgnoreCase);

            var stale = topic.Contents.Where(c => !incomingCultures.Contains(c.Culture)).ToList();
            foreach (var s in stale)
            {
                db.HelpTopicContents.Remove(s);
            }

            foreach (var c in incoming)
            {
                var culture = c.Culture.Trim();
                // Containers have no body; content pages keep their Markdown.
                var body = topic.Kind == HelpTopicKind.Container ? null : c.Body;
                var existing = topic.Contents.FirstOrDefault(e => string.Equals(e.Culture, culture, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Title = c.Title.Trim();
                    existing.Body = body;
                }
                else
                {
                    topic.Contents.Add(new HelpTopicContent { Culture = culture, Title = c.Title.Trim(), Body = body });
                }
            }
        }

        private static string ResolveTitle(List<CultureTitle> contents, string culture, string slug)
        {
            var match = HelpCulture.Resolve(culture, contents.Select(c => c.Culture));
            var title = match != null
                ? contents.FirstOrDefault(c => string.Equals(c.Culture, match, StringComparison.OrdinalIgnoreCase))?.Title
                : null;
            return string.IsNullOrWhiteSpace(title) ? slug : title!;
        }

        private sealed class CultureTitle
        {
            public string Culture { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
        }
    }
}
