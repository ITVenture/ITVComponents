using System;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem
{
    /// <summary>
    /// Shared route conventions for the help system. The resource resolver endpoint and the Markdown
    /// resource-link rewriter both build on <see cref="ResourcePrefix"/> so a <c>resource:{name}</c> embed and
    /// the served URL always line up.
    /// </summary>
    public static class HelpRoutes
    {
        /// <summary>Route prefix of the anonymous resource resolver endpoint (<c>GET {prefix}/{name}</c>).</summary>
        public const string ResourcePrefix = "/help/res";

        /// <summary>Builds the serving URL for a named resource, optionally pinned to a culture.</summary>
        public static string ResourceUrl(string name, string? culture = null)
        {
            var url = $"{ResourcePrefix}/{Uri.EscapeDataString(name)}";
            return string.IsNullOrWhiteSpace(culture) ? url : $"{url}?c={Uri.EscapeDataString(culture)}";
        }
    }
}
