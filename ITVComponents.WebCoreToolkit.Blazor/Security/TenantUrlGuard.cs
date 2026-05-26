using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Invisible root component that keeps the tenant selection sticky across in-circuit navigation and full
    /// page refreshes. Reads the configured <see cref="ScopedPermissionScopeOptions.RouteOverrideParam"/> from
    /// the current URL, remembers its value for the lifetime of the circuit, and reinjects it into every
    /// outgoing internal navigation that lacks the parameter — so the address bar always carries the active
    /// tenant and a hard refresh (F5) on any page resolves back to the same scope. The eligibility check stays
    /// with the shared <see cref="ScopedPermissionScope"/> resolution engine; this component only preserves
    /// the URL evidence of the user's choice.
    /// <para>
    /// Place a single <c>&lt;TenantUrlGuard /&gt;</c> near the application root (next to
    /// <see cref="ContextUserInitializer"/>).
    /// </para>
    /// </summary>
    public sealed class TenantUrlGuard : ComponentBase, IDisposable
    {
        [Inject] private NavigationManager Navigation { get; set; } = default!;

        [Inject] private IOptions<ScopedPermissionScopeOptions> Options { get; set; } = default!;

        private IDisposable? handlerRegistration;
        private string? currentTenant;

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            var paramName = Options.Value.RouteOverrideParam;
            if (string.IsNullOrEmpty(paramName))
            {
                return;
            }

            currentTenant = ReadTenant(Navigation.Uri, paramName);
            Navigation.LocationChanged += OnLocationChanged;
            handlerRegistration = Navigation.RegisterLocationChangingHandler(OnLocationChanging);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Navigation.LocationChanged -= OnLocationChanged;
            handlerRegistration?.Dispose();
            handlerRegistration = null;
        }

        private ValueTask OnLocationChanging(LocationChangingContext context)
        {
            var opts = Options.Value;
            var paramName = opts.RouteOverrideParam;
            if (string.IsNullOrEmpty(paramName) || string.IsNullOrEmpty(currentTenant))
            {
                return ValueTask.CompletedTask;
            }

            var target = context.TargetLocation;
            if (!IsInternal(target, out var absoluteTarget))
            {
                return ValueTask.CompletedTask;
            }

            if (IsExcludedPath(absoluteTarget.AbsolutePath, opts.AuthPathExclusions))
            {
                return ValueTask.CompletedTask;
            }

            if (HasParam(absoluteTarget.Query, paramName))
            {
                return ValueTask.CompletedTask;
            }

            var rewritten = AppendQuery(absoluteTarget, paramName, currentTenant!);
            context.PreventNavigation();
            Navigation.NavigateTo(rewritten, replace: true);
            return ValueTask.CompletedTask;
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
        {
            var paramName = Options.Value.RouteOverrideParam;
            if (string.IsNullOrEmpty(paramName))
            {
                return;
            }

            var observed = ReadTenant(args.Location, paramName);
            if (!string.IsNullOrEmpty(observed))
            {
                currentTenant = observed;
            }
        }

        private bool IsInternal(string target, out Uri absolute)
        {
            try
            {
                absolute = Navigation.ToAbsoluteUri(target);
            }
            catch
            {
                absolute = null!;
                return false;
            }

            var baseUri = new Uri(Navigation.BaseUri);
            return absolute.Scheme == baseUri.Scheme
                && string.Equals(absolute.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
                && absolute.Port == baseUri.Port;
        }

        private static bool IsExcludedPath(string absolutePath, IList<string> exclusions)
        {
            for (var i = 0; i < exclusions.Count; i++)
            {
                if (!string.IsNullOrEmpty(exclusions[i])
                    && absolutePath.StartsWith(exclusions[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasParam(string query, string paramName)
        {
            if (string.IsNullOrEmpty(query))
            {
                return false;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(idx >= 0 ? pair.Substring(0, idx) : pair);
                if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? ReadTenant(string uri, string paramName)
        {
            string query;
            try { query = new Uri(uri, UriKind.RelativeOrAbsolute).IsAbsoluteUri ? new Uri(uri).Query : ExtractRelativeQuery(uri); }
            catch { return null; }

            if (string.IsNullOrEmpty(query))
            {
                return null;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(idx >= 0 ? pair.Substring(0, idx) : pair);
                if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    var value = idx >= 0 ? Uri.UnescapeDataString(pair.Substring(idx + 1)) : string.Empty;
                    return string.IsNullOrEmpty(value) ? null : value;
                }
            }

            return null;
        }

        private static string ExtractRelativeQuery(string uri)
        {
            var qIdx = uri.IndexOf('?');
            return qIdx >= 0 ? uri.Substring(qIdx) : string.Empty;
        }

        private string AppendQuery(Uri absolute, string paramName, string value)
        {
            var separator = string.IsNullOrEmpty(absolute.Query) ? "?" : "&";
            var pair = $"{Uri.EscapeDataString(paramName)}={Uri.EscapeDataString(value)}";
            var relative = absolute.PathAndQuery + absolute.Fragment;
            return $"{relative}{separator}{pair}";
        }
    }
}
