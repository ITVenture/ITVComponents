using System;
using System.IO;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.Routing;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Rendering
{
    /// <summary>Renders help Markdown to HTML, rewriting <c>resource:</c> media embeds and <c>module:</c> links.</summary>
    public interface IHelpContentRenderer
    {
        /// <summary>
        /// Renders <paramref name="markdown"/> to HTML. <c>resource:{name}</c> / <c>/resource/{name}</c> embeds
        /// are rewritten to the anonymous resource endpoint (pinned to <paramref name="culture"/>).
        /// <c>module:{path}</c> links become a real tenant-scoped link only when <paramref name="userAuthenticated"/>
        /// is true — otherwise just the (styled) link title is emitted (no navigation for anonymous readers).
        /// </summary>
        string ToHtml(string? markdown, string? culture, bool userAuthenticated);
    }

    /// <inheritdoc />
    public class HelpContentRenderer : IHelpContentRenderer
    {
        // Advanced extensions (tables, autolinks, task lists, …). Raw inline/block HTML is DISABLED so help
        // content — shown to anonymous users — cannot inject scripts/markup via MarkupString.
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        private readonly IUrlFormat urlFormat;

        public HelpContentRenderer(IUrlFormat urlFormat)
        {
            this.urlFormat = urlFormat;
        }

        public string ToHtml(string? markdown, string? culture, bool userAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return string.Empty;
            }

            var document = Markdown.Parse(markdown, Pipeline);
            using var writer = new StringWriter();
            var renderer = new HtmlRenderer(writer);
            Pipeline.Setup(renderer);
            // Swap the default link renderer for our scheme-aware one (module: / resource:), configured per call.
            renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new HelpLinkRenderer(culture, userAuthenticated, urlFormat));
            renderer.Render(document);
            writer.Flush();
            return writer.ToString();
        }
    }

    /// <summary>
    /// Link renderer that understands the help schemes: <c>resource:</c> → the media resolver endpoint, and
    /// <c>module:</c> → an internal tenant-scoped app link that is only clickable for signed-in users.
    /// </summary>
    internal sealed class HelpLinkRenderer : LinkInlineRenderer
    {
        private readonly string? culture;
        private readonly bool authenticated;
        private readonly IUrlFormat urlFormat;

        public HelpLinkRenderer(string? culture, bool authenticated, IUrlFormat urlFormat)
        {
            this.culture = culture;
            this.authenticated = authenticated;
            this.urlFormat = urlFormat;
        }

        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            var url = link.Url ?? string.Empty;

            if (url.StartsWith("module:", StringComparison.OrdinalIgnoreCase))
            {
                var path = url.Substring("module:".Length).Trim();

                // Anonymous readers get the title text, not a link (they can't reach the module anyway).
                if (!authenticated)
                {
                    renderer.Write("<span class=\"itv-help-module-link\">");
                    renderer.WriteChildren(link);
                    renderer.Write("</span>");
                    return;
                }

                if (path.Length != 0 && !path.StartsWith("/"))
                {
                    path = "/" + path;
                }

                // Prefix with the current tenant (path-segment mode) or nothing (cookie mode) — the canonical
                // toolkit URL formatter resolves [SlashPermissionScope] to /{tenant} or "" accordingly.
                link.Url = urlFormat.FormatUrl("[SlashPermissionScope]" + path);
                base.Write(renderer, link);
                return;
            }

            if (TryResourceName(url, out var name))
            {
                // Same tenant-scoping as module: links — the resource endpoint is reached under the current
                // tenant path segment (/{tenant}/help/res/...), so embedded media resolves against the tenant the
                // reader is on. [SlashPermissionScope] expands to /{tenant} (path-segment mode) or "" (cookie mode).
                link.Url = urlFormat.FormatUrl("[SlashPermissionScope]" + HelpRoutes.ResourceUrl(name, culture));
            }

            base.Write(renderer, link);
        }

        private static bool TryResourceName(string url, out string name)
        {
            name = string.Empty;
            string raw;
            if (url.StartsWith("resource:", StringComparison.OrdinalIgnoreCase))
            {
                raw = url.Substring("resource:".Length);
            }
            else if (url.StartsWith("/resource/", StringComparison.OrdinalIgnoreCase))
            {
                raw = url.Substring("/resource/".Length);
            }
            else
            {
                return false;
            }

            raw = raw.Trim().TrimStart('/');
            var query = raw.IndexOf('?');
            if (query >= 0)
            {
                raw = raw.Substring(0, query);
            }

            if (raw.Length == 0)
            {
                return false;
            }

            name = raw;
            return true;
        }
    }
}
