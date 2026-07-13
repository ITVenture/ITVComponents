using System;
using System.IO;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Rendering
{
    /// <summary>Renders help Markdown to HTML, rewriting <c>resource:{name}</c> embeds to the resolver URL.</summary>
    public interface IHelpContentRenderer
    {
        /// <summary>
        /// Renders <paramref name="markdown"/> to HTML. Link/image URLs of the form <c>resource:{name}</c> or
        /// <c>/resource/{name}</c> are rewritten to the anonymous resource endpoint, pinned to
        /// <paramref name="culture"/> so the served media matches the rendered content's language.
        /// </summary>
        string ToHtml(string? markdown, string? culture);
    }

    /// <inheritdoc />
    public class HelpContentRenderer : IHelpContentRenderer
    {
        // Advanced extensions (tables, autolinks, task lists, …). Raw inline/block HTML is DISABLED so that
        // help content — which is shown to anonymous users — cannot inject scripts/markup via MarkupString.
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        public string ToHtml(string? markdown, string? culture)
        {
            if (string.IsNullOrWhiteSpace(markdown))
            {
                return string.Empty;
            }

            var document = Markdown.Parse(markdown, Pipeline);
            foreach (var link in document.Descendants<LinkInline>())
            {
                if (TryRewriteResourceUrl(link.Url, culture, out var rewritten))
                {
                    link.Url = rewritten;
                }
            }

            using var writer = new StringWriter();
            var renderer = new HtmlRenderer(writer);
            Pipeline.Setup(renderer);
            renderer.Render(document);
            writer.Flush();
            return writer.ToString();
        }

        private static bool TryRewriteResourceUrl(string? url, string? culture, out string result)
        {
            result = string.Empty;
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            string name;
            if (url.StartsWith("resource:", StringComparison.OrdinalIgnoreCase))
            {
                name = url.Substring("resource:".Length);
            }
            else if (url.StartsWith("/resource/", StringComparison.OrdinalIgnoreCase))
            {
                name = url.Substring("/resource/".Length);
            }
            else
            {
                return false;
            }

            name = name.Trim().TrimStart('/');
            var query = name.IndexOf('?');
            if (query >= 0)
            {
                name = name.Substring(0, query);
            }

            if (name.Length == 0)
            {
                return false;
            }

            result = HelpRoutes.ResourceUrl(name, culture);
            return true;
        }
    }
}
