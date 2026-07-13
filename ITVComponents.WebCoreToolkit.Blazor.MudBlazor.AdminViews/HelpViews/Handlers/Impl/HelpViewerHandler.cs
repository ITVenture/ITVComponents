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
                    Contents = t.Contents.Select(c => new CultureTitle { Culture = c.Culture, Title = c.Title }).ToList()
                })
                .ToListAsync(ct);

            var present = new HashSet<int>(topics.Select(t => t.HelpTopicId));
            var byParent = topics
                .GroupBy(t => t.ParentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // A published child of an UNpublished parent has no reachable path, so treat its parent as absent and
            // hang it at the root rather than losing it entirely.
            List<HelpTreeNodeViewModel> Build(int? parentId)
            {
                if (!byParent.TryGetValue(parentId, out var children))
                {
                    return new List<HelpTreeNodeViewModel>();
                }

                return children.Select(t => new HelpTreeNodeViewModel
                {
                    HelpTopicId = t.HelpTopicId,
                    Slug = t.Slug,
                    Icon = t.Icon,
                    Kind = t.Kind,
                    Title = ResolveTitle(t.Contents, uiCulture, t.Slug),
                    Children = Build(t.HelpTopicId)
                }).ToList();
            }

            var roots = Build(null);
            foreach (var kvp in byParent)
            {
                if (kvp.Key.HasValue && !present.Contains(kvp.Key.Value))
                {
                    roots.AddRange(kvp.Value.Select(t => new HelpTreeNodeViewModel
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
