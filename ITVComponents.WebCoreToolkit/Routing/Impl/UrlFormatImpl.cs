using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Routing.Impl
{
    public class UrlFormatImpl:IUrlFormat
    {
        private readonly IContextUserProvider userProvider;
        private readonly IPermissionScope permissionScope;
        private readonly ISharedAssetContext assetContext;
        private readonly IAppLink appLink;

        /// <summary>
        /// Initializes a new instance of the <see cref="UrlFormatImpl"/> class.
        /// </summary>
        /// <param name="userProvider">the ambient context whose route data feeds the placeholders</param>
        /// <param name="permissionScope">the current scope</param>
        /// <param name="assetContext">the shared asset of the current context, when there is one</param>
        /// <param name="appLink">the host's link builder; supplies the culture prefix, which is the one part
        /// of a root-absolute prefix that can not be read off the scope or the asset</param>
        public UrlFormatImpl(IContextUserProvider userProvider, IPermissionScope permissionScope,
            ISharedAssetContext assetContext, IAppLink appLink = null)
        {
            this.userProvider = userProvider;
            this.permissionScope = permissionScope;
            this.assetContext = assetContext;
            this.appLink = appLink;
        }

        /// <summary>
        /// Formats a url using the given url-prototype. Use [paramSlash] to access a parameter suffixed with a slash or [Slashparam] to access the parameter prefixed with a slash
        /// </summary>
        /// <remarks>
        /// Two families of scope placeholders, because two kinds of caller need two different answers:
        /// <list type="bullet">
        /// <item><c>[permissionScope]</c> / <c>[SlashPermissionScope]</c> yield the <b>full</b> prefix
        /// (culture, shared-asset segment and tenant, in that order) and belong in root-absolute output such
        /// as an <c>href</c>.</item>
        /// <item><c>[scopeUnderBase]</c> / <c>[SlashScopeUnderBase]</c> yield only what is <b>not</b> already
        /// in <c>PathBase</c> and belong behind a <c>~</c>, which the client script resolves against the
        /// base url. Using the first family there would prepend the prefix twice.</item>
        /// </list>
        /// <c>[assetSegment]</c> / <c>[SlashAssetSegment]</c> expose the asset segment alone.
        /// </remarks>
        /// <param name="url">the route prototype for a route that needs to be formatted</param>
        /// <returns>the formatted url.</returns>
        public string FormatUrl(string url)
        {
            Dictionary<string, object> values = new Dictionary<string, object>();
            foreach (var arg in userProvider.RouteData)
            {
                if (arg.Value != null && (!(arg.Value is string) || !string.IsNullOrEmpty((string) arg.Value)))
                {
                    values.Add(arg.Key, arg.Value);
                    values.Add($"{arg.Key}Slash", $"{arg.Value}/");
                    values.Add($"Slash{arg.Key}", $"/{arg.Value}");
                }
            }

            var assetSegment = assetContext?.Segment;
            if (!string.IsNullOrEmpty(assetSegment))
            {
                values.Add("assetSegment", assetSegment);
                values.Add("assetSegmentSlash", $"{assetSegment}/");
                values.Add("SlashAssetSegment", $"/{assetSegment}");
            }

            var tenant = permissionScope.IsScopeExplicit ? permissionScope.PermissionPrefix : null;
            // Die Sprache fuehrt den root-absoluten Praefix an - vor Asset und Mandant, so wie sie in der URL
            // steht. Hinter einem ~ hat sie nichts zu suchen: dort loest der Aufrufer gegen die Basis auf,
            // und die traegt sie bereits. Sie steht auch dann alleine im Praefix, wenn es weder Asset noch
            // Mandant gibt - sonst bliebe der Platzhalter in einer reinen Sprach-Installation unersetzt.
            var culturePrefix = appLink?.CulturePrefix ?? string.Empty;
            if (!string.IsNullOrEmpty(tenant) || !string.IsNullOrEmpty(assetSegment) || culturePrefix.Length != 0)
            {
                var full = culturePrefix + SharedAssetPath.BuildPrefix(assetSegment, tenant);
                var underBase = ScopeUnderBase(tenant);
                values.Add("permissionScope", full.TrimStart('/'));
                values.Add("permissionScopeSlash", $"{full.TrimStart('/')}/");
                values.Add("SlashPermissionScope", full);
                values.Add("scopeUnderBase", underBase.TrimStart('/'));
                values.Add("scopeUnderBaseSlash", underBase.Length != 0 ? $"{underBase.TrimStart('/')}/" : string.Empty);
                values.Add("SlashScopeUnderBase", underBase);
            }

            return values.FormatText(url);
        }

        /// <summary>
        /// Determines the part of the prefix a caller still has to prepend after <c>~</c> resolved to the
        /// base url. The asset segment always sits in <c>PathBase</c>; the tenant only does so on hosts that
        /// strip it from the path (Blazor's path-segment mode), while it stays a route value elsewhere.
        /// </summary>
        /// <param name="tenant">the current scope, or null</param>
        /// <returns>the remaining prefix with a leading slash, or an empty string</returns>
        private string ScopeUnderBase(string tenant)
        {
            if (string.IsNullOrEmpty(tenant))
            {
                return string.Empty;
            }

            var pathBase = (userProvider as IHttpContextUserProvider)?.HttpContext?.Request.PathBase.Value;
            if (string.IsNullOrEmpty(pathBase))
            {
                // Kein lebender Request (Blazor-Circuit): dort traegt der base-href den ganzen Praefix, ein
                // ~-Aufrufer existiert nicht. Nichts voranstellen ist die sichere Antwort.
                return userProvider is IHttpContextUserProvider ? $"/{tenant}" : string.Empty;
            }

            return pathBase.EndsWith($"/{tenant}", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"/{tenant}";
        }
    }
}
