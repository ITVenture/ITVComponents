using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Invisible root component that keeps the tenant selection sticky across in-circuit navigation and full
    /// page refreshes. Two modes, driven by <see cref="ScopedPermissionScopeOptions.TenantSource"/>:
    /// <list type="bullet">
    ///   <item><description><b>Query</b>: remembers the last value of <c>?tenant=…</c> seen in the URL and
    ///   re-injects it into every outgoing internal navigation that lacks the parameter.</description></item>
    ///   <item><description><b>PathSegment</b>: relies on the host emitting <c>&lt;base href="/{tenant}/"&gt;</c>
    ///   so relative navigations automatically carry the prefix; only catches absolute-path navigations
    ///   (e.g. <c>NavigateTo("/users")</c>) that would escape the tenant prefix and rewrites them under the
    ///   current base path.</description></item>
    /// </list>
    /// In both modes auth-relevant paths (<see cref="ScopedPermissionScopeOptions.AuthPathExclusions"/>) are
    /// left untouched, and the eligibility check stays with the shared <see cref="ScopedPermissionScope"/>
    /// resolution engine — this component only preserves the URL evidence of the user's choice.
    /// <para>
    /// Place a single <c>&lt;TenantUrlGuard /&gt;</c> near the application root (next to
    /// <see cref="ContextUserInitializer"/>).
    /// </para>
    /// <para>
    /// <b>Reach — and where it structurally ends.</b> The guard hooks
    /// <c>RegisterLocationChangingHandler</c>, so it only ever sees navigations that stay <em>inside the
    /// circuit</em>: every programmatic <c>NavigateTo</c> (including <c>forceLoad</c>, the handlers run
    /// before the JS interop), and those anchor clicks that Blazor's JS intercepts — which it does only for
    /// targets <em>within the base-URI space</em>. A root-absolute <c>&lt;a href="/Foo"&gt;</c> under
    /// <c>&lt;base href="/{tenant}/"&gt;</c> resolves outside that space, so the browser performs an ordinary
    /// document load: the click never reaches the circuit, the old circuit is already gone when the request
    /// arrives, and <b>no code in this component can rewrite it</b> — the request reaches
    /// <see cref="TenantPathPrefixMiddleware"/> with a first segment that is not an eligible scope and yields
    /// a 404. The same holds for form posts and <c>window.location</c> assignments. Do not read the guard as
    /// a safety net against those; when such a 404 shows up, the defect is in the emitted link, not in the
    /// host wiring.
    /// </para>
    /// <para>
    /// <b>Rule for view packages:</b> emit navigation targets <em>relative</em> (no leading slash) so they
    /// resolve against the base href — that works in both modes and at any URL depth, because the base href
    /// always ends in a slash. A leading slash is only correct for deliberately tenant-neutral paths, i.e.
    /// those covered by <see cref="ScopedPermissionScopeOptions.AuthPathExclusions"/>.
    /// </para>
    /// </summary>
    public sealed class TenantUrlGuard : ComponentBase, IDisposable
    {
        [Inject] private NavigationManager Navigation { get; set; } = default!;

        [Inject] private IOptions<ScopedPermissionScopeOptions> Options { get; set; } = default!;

        [Inject] private IContextUserProvider ContextUser { get; set; } = default!;

        private static readonly HashSet<string> EmptyScopes = new HashSet<string>(StringComparer.Ordinal);

        private IDisposable? handlerRegistration;
        private string? currentTenant;
        private string? basePath;
        private ClaimsPrincipal? cachedPrincipal;
        private HashSet<string>? cachedEligibles;

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            var opts = Options.Value;
            var paramName = opts.RouteOverrideParam;
            if (string.IsNullOrEmpty(paramName))
            {
                return;
            }

            if (opts.TenantSource == TenantSource.PathSegment)
            {
                basePath = ExtractBasePath(Navigation.BaseUri);
            }
            else
            {
                currentTenant = ReadTenantQuery(Navigation.Uri, paramName);
                Navigation.LocationChanged += OnLocationChanged;
            }

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
            if (string.IsNullOrEmpty(opts.RouteOverrideParam))
            {
                return ValueTask.CompletedTask;
            }

            return opts.TenantSource == TenantSource.PathSegment
                ? HandlePathSegment(context, opts)
                : HandleQuery(context, opts);
        }

        private ValueTask HandlePathSegment(LocationChangingContext context, ScopedPermissionScopeOptions opts)
        {
            if (string.IsNullOrEmpty(basePath) || basePath == "/")
            {
                return ValueTask.CompletedTask;
            }

            if (!TryToAbsolute(context.TargetLocation, out var absolute) || !IsSameOrigin(absolute))
            {
                return ValueTask.CompletedTask;
            }

            var rewritten = TenantUrlGuardLogic.PlanPathSegmentRewrite(
                basePath!,
                absolute,
                opts.AuthPathExclusions,
                GetEligibleScopes());

            if (rewritten == null)
            {
                return ValueTask.CompletedTask;
            }

            context.PreventNavigation();
            Navigation.NavigateTo(rewritten, replace: true);
            return ValueTask.CompletedTask;
        }

        private ISet<string> GetEligibleScopes()
        {
            var user = ContextUser.User;
            if (!ReferenceEquals(user, cachedPrincipal))
            {
                cachedPrincipal = user;
                cachedEligibles = ComputeEligibles(user);
            }
            return cachedEligibles ?? (ISet<string>)EmptyScopes;
        }

        private HashSet<string> ComputeEligibles(ClaimsPrincipal? user)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var services = ContextUser.Services;
            var mapper = services?.GetService<IUserNameMapper>();
            var repo = services?.GetService<ISecurityRepository>();
            if (mapper == null || repo == null)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            return TenantPathPrefixMiddleware.ResolveEligibleScopes(user, mapper, repo)
                .Select(s => s.ScopeName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet(StringComparer.Ordinal);
        }

        private ValueTask HandleQuery(LocationChangingContext context, ScopedPermissionScopeOptions opts)
        {
            var paramName = opts.RouteOverrideParam!;
            if (string.IsNullOrEmpty(currentTenant))
            {
                return ValueTask.CompletedTask;
            }

            if (!TryToAbsolute(context.TargetLocation, out var absolute) || !IsSameOrigin(absolute))
            {
                return ValueTask.CompletedTask;
            }

            if (IsExcludedPath(absolute.AbsolutePath, opts.AuthPathExclusions))
            {
                return ValueTask.CompletedTask;
            }

            if (HasParam(absolute.Query, paramName))
            {
                return ValueTask.CompletedTask;
            }

            var separator = string.IsNullOrEmpty(absolute.Query) ? "?" : "&";
            var pair = $"{Uri.EscapeDataString(paramName)}={Uri.EscapeDataString(currentTenant!)}";
            var rewritten = absolute.PathAndQuery + absolute.Fragment + separator + pair;
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

            var observed = ReadTenantQuery(args.Location, paramName);
            if (!string.IsNullOrEmpty(observed))
            {
                currentTenant = observed;
            }
        }

        private bool TryToAbsolute(string target, out Uri absolute)
        {
            try
            {
                absolute = Navigation.ToAbsoluteUri(target);
                return true;
            }
            catch
            {
                absolute = null!;
                return false;
            }
        }

        private bool IsSameOrigin(Uri absolute)
        {
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

        private static string? ReadTenantQuery(string uri, string paramName)
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

        private static string ExtractBasePath(string baseUri)
        {
            try
            {
                var path = new Uri(baseUri).AbsolutePath;
                return string.IsNullOrEmpty(path) ? "/" : path;
            }
            catch
            {
                return "/";
            }
        }
    }
}
