using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Rendering;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers.Impl
{
    /// <summary>
    /// Default <see cref="IHelpViewerHandler"/> for the public (anonymous) viewer. Reads only published topics
    /// and renders their localized Markdown to HTML via <see cref="IHelpContentRenderer"/>. Uses a per-operation
    /// context; no permission or tenant scope is required.
    /// </summary>
    public class HelpViewerHandler<TContext> : IHelpViewerHandler
        where TContext : DbContext, IHelpSystemContext
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IHelpContentRenderer renderer;

        public HelpViewerHandler(IDbContextFactory<TContext> dbFactory, IHelpContentRenderer renderer)
        {
            this.dbFactory = dbFactory;
            this.renderer = renderer;
        }

        public async Task<HelpTreeNodeViewModel[]> GetPublishedTreeAsync(string? culture, CancellationToken ct = default)
        {
            var uiCulture = string.IsNullOrWhiteSpace(culture) ? CultureInfo.CurrentUICulture.Name : culture;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var topics = await db.HelpTopics.AsNoTracking()
                .Where(t => t.IsPublished)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Slug)
                .Select(t => new
                {
                    t.HelpTopicId,
                    t.ParentId,
                    t.Kind,
                    t.Slug,
                    t.Icon,
                    t.ShowInMenu,
                    Contents = t.Contents.Select(c => new CultureTitle { Culture = c.Culture, Title = c.Title }).ToList()
                })
                .ToListAsync(ct);

            var present = new HashSet<int>(topics.Select(t => t.HelpTopicId));
            // Group by parent, mapping "no parent" (root) to the sentinel 0 — HelpTopicId identities start at 1,
            // so 0 never collides. Avoids an int?-keyed dictionary (null key / ToDictionary's notnull constraint).
            const int rootKey = 0;
            var byParent = topics
                .GroupBy(t => t.ParentId ?? rootKey)
                .ToDictionary(g => g.Key, g => g.ToList());

            // A published child of an UNpublished parent has no reachable path, so treat its parent as absent and
            // hang it at the root rather than losing it entirely.
            //
            // ShowInMenu is filtered HERE and not in the query above: dropping those rows early would turn the
            // children of a hidden container into orphans, and the rescue below would hang them at the root —
            // exactly the documents the flag was meant to keep out of the menu. Filtering while building drops
            // the branch as a whole, which is what "hidden" has to mean for a tree.
            List<HelpTreeNodeViewModel> Build(int parentKey)
            {
                if (!byParent.TryGetValue(parentKey, out var children))
                {
                    return new List<HelpTreeNodeViewModel>();
                }

                return children.Where(t => t.ShowInMenu).Select(t => new HelpTreeNodeViewModel
                {
                    HelpTopicId = t.HelpTopicId,
                    Slug = t.Slug,
                    Icon = t.Icon,
                    Kind = t.Kind,
                    Title = ResolveTitle(t.Contents, uiCulture, t.Slug),
                    Children = Build(t.HelpTopicId)
                }).ToList();
            }

            var roots = Build(rootKey);
            foreach (var kvp in byParent)
            {
                if (kvp.Key != rootKey && !present.Contains(kvp.Key))
                {
                    roots.AddRange(kvp.Value.Where(t => t.ShowInMenu).Select(t => new HelpTreeNodeViewModel
                    {
                        HelpTopicId = t.HelpTopicId,
                        Slug = t.Slug,
                        Icon = t.Icon,
                        Kind = t.Kind,
                        Title = ResolveTitle(t.Contents, uiCulture, t.Slug),
                        Children = Build(t.HelpTopicId)
                    }));
                }
            }

            return roots.ToArray();
        }

        public async Task<HelpTreeNodeViewModel?> GetPublishedSubtreeAsync(string slug, string? culture, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            var uiCulture = string.IsNullOrWhiteSpace(culture) ? CultureInfo.CurrentUICulture.Name : culture;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var topics = await db.HelpTopics.AsNoTracking()
                .Where(t => t.IsPublished)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Slug)
                .Select(t => new
                {
                    t.HelpTopicId,
                    t.ParentId,
                    t.Kind,
                    t.Slug,
                    t.Icon,
                    t.ShowInMenu,
                    Contents = t.Contents.Select(c => new CultureTitle { Culture = c.Culture, Title = c.Title }).ToList()
                })
                .ToListAsync(ct);

            var root = topics.FirstOrDefault(t => t.Slug == slug);
            if (root == null)
            {
                return null;
            }

            const int rootKey = 0;
            var byParent = topics
                .GroupBy(t => t.ParentId ?? rootKey)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Die Wurzel wurde ausdruecklich per Slug angefragt und wird darum immer geliefert, auch wenn sie
            // nicht im Menue steht - genau so haengt die Kontexthilfe an einem ausgeblendeten Bereich. Innerhalb
            // des Teilbaums gilt das Flag dagegen wie ueberall: was nicht gelistet werden soll, erscheint auch
            // in dieser Navigation nicht.
            List<HelpTreeNodeViewModel> Build(int parentKey)
            {
                if (!byParent.TryGetValue(parentKey, out var children))
                {
                    return new List<HelpTreeNodeViewModel>();
                }

                return children.Where(t => t.ShowInMenu).Select(t => new HelpTreeNodeViewModel
                {
                    HelpTopicId = t.HelpTopicId,
                    Slug = t.Slug,
                    Icon = t.Icon,
                    Kind = t.Kind,
                    Title = ResolveTitle(t.Contents, uiCulture, t.Slug),
                    Children = Build(t.HelpTopicId)
                }).ToList();
            }

            return new HelpTreeNodeViewModel
            {
                HelpTopicId = root.HelpTopicId,
                Slug = root.Slug,
                Icon = root.Icon,
                Kind = root.Kind,
                Title = ResolveTitle(root.Contents, uiCulture, root.Slug),
                Children = Build(root.HelpTopicId)
            };
        }

        public async Task<HelpTopicViewViewModel?> GetPublishedTopicAsync(string slug, string? culture, bool userAuthenticated, CancellationToken ct = default)
        {
            var uiCulture = string.IsNullOrWhiteSpace(culture) ? CultureInfo.CurrentUICulture.Name : culture;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var topic = await db.HelpTopics.AsNoTracking().Include(t => t.Contents)
                .FirstOrDefaultAsync(t => t.IsPublished && t.Slug == slug, ct);
            if (topic == null)
            {
                return null;
            }

            var match = HelpCulture.Resolve(uiCulture, topic.Contents.Select(c => c.Culture));
            var content = match != null
                ? topic.Contents.FirstOrDefault(c => c.Culture == match)
                : null;

            return new HelpTopicViewViewModel
            {
                HelpTopicId = topic.HelpTopicId,
                Slug = topic.Slug,
                Kind = topic.Kind,
                Title = string.IsNullOrWhiteSpace(content?.Title) ? topic.Slug : content!.Title,
                Html = topic.Kind == HelpTopicKind.Container ? string.Empty : renderer.ToHtml(content?.Body, match, userAuthenticated)
            };
        }

        public async Task<bool> PublishedTopicExistsAsync(string slug, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return false;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var topic = await db.HelpTopics.AsNoTracking()
                .Where(t => t.IsPublished && t.Slug == slug)
                .Select(t => new { t.HelpTopicId, t.Kind })
                .FirstOrDefaultAsync(ct);
            if (topic == null)
            {
                return false;
            }

            // Ein Inhaltsthema hat immer etwas zu zeigen; ein Container nur dann, wenn er (veroeffentlichte)
            // Kinder hat - dann bietet das Popup den Teilbaum als Navigation an. Ein leerer Container hat
            // nichts anzuzeigen und die Kontexthilfe bleibt (wie bisher) unsichtbar.
            return topic.Kind == HelpTopicKind.ContentPage
                   || await db.HelpTopics.AsNoTracking()
                       .AnyAsync(t => t.IsPublished && t.ParentId == topic.HelpTopicId, ct);
        }

        private static string ResolveTitle(List<CultureTitle> contents, string culture, string slug)
        {
            var match = HelpCulture.Resolve(culture, contents.Select(c => c.Culture));
            var title = match != null
                ? contents.FirstOrDefault(c => c.Culture == match)?.Title
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
