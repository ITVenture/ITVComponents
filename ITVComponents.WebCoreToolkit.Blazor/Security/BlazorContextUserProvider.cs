using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Circuit-scoped <see cref="IContextUserProvider"/> for Blazor. Because the DI scope lives for the whole
    /// circuit, one instance == one browser tab — so different tabs naturally carry different ambient state.
    /// The (synchronous) <see cref="User"/> getter is served from a cached principal that is seeded once by
    /// <see cref="ContextUserInitializer"/> and kept current via <see cref="AuthenticationStateProvider"/>'s
    /// change notification. On static-SSR requests (no circuit) it falls back to the request's
    /// <c>HttpContext.User</c>. Render-mode-neutral (Server and WebAssembly), no CircuitHandler dependency.
    /// </summary>
    public sealed class BlazorContextUserProvider : IContextUserProvider, IDisposable
    {
        private static readonly ClaimsPrincipal Anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        private readonly AuthenticationStateProvider authStateProvider;
        private readonly NavigationManager navigation;
        private readonly IOptions<ScopedPermissionScopeOptions> scopeOptions;
        private readonly IHttpContextAccessor httpContextAccessor;
        private ClaimsPrincipal user = Anonymous;

        public BlazorContextUserProvider(AuthenticationStateProvider authStateProvider, NavigationManager navigation, IServiceProvider services, IOptions<ScopedPermissionScopeOptions> scopeOptions, IHttpContextAccessor httpContextAccessor)
        {
            this.authStateProvider = authStateProvider;
            this.navigation = navigation;
            this.scopeOptions = scopeOptions;
            this.httpContextAccessor = httpContextAccessor;
            Services = services;
            authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        /// <inheritdoc/>
        public ClaimsPrincipal User
        {
            get
            {
                // A live circuit seeds `user` (via ContextUserInitializer) and keeps it current through
                // AuthenticationStateChanged, so prefer it whenever it carries an authenticated identity.
                if (user.Identity?.IsAuthenticated == true)
                {
                    return user;
                }

                // Static SSR (e.g. [ExcludeFromInteractiveRouting] Identity pages) renders without a circuit:
                // the ServerAuthenticationStateProvider-derived AuthenticationStateProvider never gets its state
                // set, so both the seed and the change-event yield Anonymous. The HttpContext is available
                // exactly in that window (and null inside a live circuit), so fall back to the request's
                // authenticated user — otherwise scope resolution and navigation see an anonymous user on
                // every Identity page.
                var contextUser = httpContextAccessor.HttpContext?.User;
                return contextUser?.Identity?.IsAuthenticated == true ? contextUser : user;
            }
        }

        /// <inheritdoc/>
        public IServiceProvider Services { get; }

        /// <inheritdoc/>
        /// <remarks>
        /// The tenant normally comes from the base URI, i.e. from the <c>&lt;base href&gt;</c> — which only
        /// exists once Blazor renders. Endpoints that render no component (the toolkit's own
        /// <c>/{tenant}/Diagnostics</c>, <c>/ForeignKey</c> and <c>/DBW</c> are minimal-API <c>MapGet</c>s)
        /// have neither a base href nor an initialized NavigationManager, so this getter used to yield no
        /// tenant at all for them: scope resolution found no route override, fell back to the user's default
        /// tenant and served that tenant's data — silently, and under a URL that says otherwise.
        /// <para>
        /// Hence the fallback to the request, in two steps. First
        /// <see cref="TenantPathPrefixMiddleware.TenantSegmentItemKey"/>: a host running
        /// <c>UseTenantPathPrefix()</c> strips the tenant segment off <c>Request.Path</c> and appends it to
        /// <c>PathBase</c> <em>before</em> routing, so the tenant can never reach the route values there —
        /// not "mostly not", but never (BUG-PRE187). The middleware stashes the validated segment in
        /// <see cref="HttpContext.Items"/>, which is where <see cref="TenantBaseHref"/> already reads it
        /// from. Deliberately that stash and not the first segment of <c>PathBase</c>: the middleware
        /// <em>appends</em>, so under a virtual directory the tenant would be the last segment, not the first.
        /// Then the request's route values, the very source
        /// <see cref="ITVComponents.WebCoreToolkit.Security.DefaultContextUserProvider"/> reads outside
        /// Blazor — still right for hosts without that middleware. Same move the <see cref="User"/> getter
        /// already makes for the same gap. The base URI stays first: inside a live circuit it is the correct
        /// source (and the HttpContext is null there anyway), so per-tab behaviour is untouched — the
        /// HttpContext is strictly the fallback.
        /// </para>
        /// <para>
        /// Deliberately no reading of the request's query: on a non-Blazor host these endpoints see exactly
        /// the route values, and a <c>?tenant=</c> must not become an override channel that the same endpoint
        /// would not have without Blazor. The middleware's value carries no such risk — it is only set after
        /// the segment was validated against the user's eligible scopes (a foreign one 404s), and
        /// <c>ResolvingPermissionScope</c> checks again anyway.
        /// </para>
        /// </remarks>
        public IDictionary<string, object> RouteData
        {
            get
            {
                // Outside a live circuit/request — e.g. plugin initialization at startup (UsePluginsInit),
                // where a permission-scoped query forces scope resolution — the scoped NavigationManager is
                // not yet initialized and throws ('…NavigationManager has not been initialized') on Uri/BaseUri.
                // That window has no HttpContext either, so the fallback below yields nothing and scope
                // resolution still lands on its default. A served request, however, does have one.
                string uri, baseUri;
                try
                {
                    uri = navigation.Uri;
                    baseUri = navigation.BaseUri;
                }
                catch (InvalidOperationException)
                {
                    return RequestValues();
                }

                var result = ParseQuery(uri);
                var baseSegments = BaseSegments(baseUri);
                // Der Asset-Abschnitt fuehrt die URL an (siehe SharedAssetPath): steht er im base-href, ist
                // der Mandant der ZWEITE Abschnitt, nicht der erste. Und ein lebender Circuit hat keinen
                // HttpContext mehr - der Abschnitt hier ist die einzige Quelle, aus der der Asset-Kontext
                // eine Navigation im Circuit ueberlebt.
                var assetSegment = baseSegments.Length != 0 && SharedAssetPath.IsAssetSegment(baseSegments[0])
                    ? baseSegments[0]
                    : RequestAssetSegment();
                if (!string.IsNullOrEmpty(assetSegment))
                {
                    result[Global.SharedAssetSegmentItemKey] = assetSegment;
                }

                var opts = scopeOptions.Value;
                if (opts.TenantSource == TenantSource.PathSegment
                    && !string.IsNullOrEmpty(opts.RouteOverrideParam))
                {
                    var tenantIndex = baseSegments.Length != 0 && SharedAssetPath.IsAssetSegment(baseSegments[0]) ? 1 : 0;
                    var segment = baseSegments.Length > tenantIndex ? baseSegments[tenantIndex] : null;
                    if (string.IsNullOrEmpty(segment))
                    {
                        // NavigationManager initialized but the base URI carries no tenant — a request that
                        // renders nothing can land here too, depending on how the host wires Blazor.
                        segment = RequestTenantSegment() ?? RequestRouteValue(opts.RouteOverrideParam!);
                    }

                    if (!string.IsNullOrEmpty(segment))
                    {
                        result[opts.RouteOverrideParam!] = segment;
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// What the request being served knows: its route values, with the tenant segment stashed by
        /// <see cref="TenantPathPrefixMiddleware"/> laid over them. A copy, not the request's own
        /// <c>RouteValueDictionary</c> — this getter's result is handed out to be read, and writing the
        /// tenant into the live route values of the request would be a side effect of reading.
        /// </summary>
        private IDictionary<string, object> RequestValues()
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var routeValues = httpContextAccessor.HttpContext?.GetRouteData()?.Values;
            if (routeValues != null)
            {
                foreach (var pair in routeValues)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            var param = scopeOptions.Value.RouteOverrideParam;
            if (!string.IsNullOrEmpty(param) && RequestTenantSegment() is { } segment)
            {
                result[param!] = segment;
            }

            return result;
        }

        /// <summary>
        /// The tenant segment <see cref="TenantPathPrefixMiddleware"/> validated and stashed for the request
        /// being served, or <c>null</c> when there is no request or the host does not run that middleware.
        /// </summary>
        private string? RequestTenantSegment()
            => httpContextAccessor.HttpContext?.Items.TryGetValue(TenantPathPrefixMiddleware.TenantSegmentItemKey, out var value) == true
                ? value as string
                : null;

        /// <summary>
        /// The shared-asset segment <see cref="SharedAssetPathMiddleware"/> stashed for the request being
        /// served, or <c>null</c>. Only relevant while a request exists (static SSR, non-Blazor endpoints);
        /// inside a live circuit the base URI is the source.
        /// </summary>
        private string? RequestAssetSegment()
            => httpContextAccessor.HttpContext?.Items.TryGetValue(Global.SharedAssetSegmentItemKey, out var value) == true
                ? value as string
                : null;

        /// <summary>
        /// A single route value of the request being served, or <c>null</c> when there is no request (live
        /// circuit, or the startup window before any request) or the route does not carry that parameter.
        /// </summary>
        private string? RequestRouteValue(string key)
            => httpContextAccessor.HttpContext?.GetRouteData()?.Values.TryGetValue(key, out var value) == true
                ? value as string
                : null;

        /// <inheritdoc/>
        /// <remarks>
        /// Same gap as in <see cref="RouteData"/>, same fallback: without a circuit there is no
        /// NavigationManager URI, but a served request knows its path. Reached e.g. when request data is
        /// conserved for background work started from one of the non-Blazor endpoints.
        /// <para>
        /// <c>PathBase</c> goes in front of <c>Path</c> on purpose. Under <c>UseTenantPathPrefix()</c> the
        /// tenant segment has been moved from the one to the other before routing, and <c>Request.Path</c>
        /// alone would report <c>/diagnostics/Q</c> for a caller who requested <c>/TenantA/diagnostics/Q</c> —
        /// a path that no longer identifies what was asked for, and that resolves to a different tenant when
        /// replayed. The circuit branch above has the whole path (the base href carries the prefix), so
        /// joining the two here is also what keeps both branches answering the same question.
        /// </para>
        /// </remarks>
        public string RequestPath
        {
            get
            {
                try { return new Uri(navigation.Uri).AbsolutePath; }
                catch (InvalidOperationException)
                {
                    var request = httpContextAccessor.HttpContext?.Request;
                    return request == null ? null : request.PathBase.Add(request.Path).Value;
                }
            }
        }

        /// <summary>
        /// Seeds the cached principal. Called once per circuit by <see cref="ContextUserInitializer"/> after the
        /// first interactive render, where the (async) authentication-state can be awaited.
        /// </summary>
        /// <param name="principal">the principal of the current circuit</param>
        public void Seed(ClaimsPrincipal principal) => user = principal ?? Anonymous;

        private void OnAuthenticationStateChanged(Task<AuthenticationState> task) => _ = ApplyAsync(task);

        private async Task ApplyAsync(Task<AuthenticationState> task)
        {
            try
            {
                var state = await task;
                user = state.User ?? Anonymous;
            }
            catch
            {
                // a failed revalidation must not crash the circuit; keep the previous principal
            }
        }

        /// <summary>
        /// The segments of the current base URI, in order. Formerly only the first one was needed (the
        /// tenant); with a shared asset in the path the base href can carry two prefixes, and which is which
        /// depends on the first one being marked.
        /// </summary>
        private static string[] BaseSegments(string baseUri)
        {
            try
            {
                var path = new Uri(baseUri).AbsolutePath;
                if (string.IsNullOrEmpty(path)) return Array.Empty<string>();
                // Die Kultur fuehrt den base-href an, noch vor dem Asset (siehe CulturePath). Sie kommt hier
                // ab, damit die Zaehlung dahinter dieselbe bleibt: die Aufrufer fragen nach dem ERSTEN
                // Abschnitt und meinen das Asset bzw. den Mandanten. Ohne diesen Schnitt wuerde auf einem
                // Host mit Sprach-Praefix ploetzlich "de-CH" als Mandant gelten.
                path = CulturePath.Strip(path);
                var trimmed = path.Trim('/');
                if (trimmed.Length == 0) return Array.Empty<string>();
                return trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IDictionary<string, object> ParseQuery(string uri)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string query;
            try { query = new Uri(uri).Query; }
            catch { return result; }

            if (string.IsNullOrEmpty(query))
            {
                return result;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(idx >= 0 ? pair.Substring(0, idx) : pair);
                var value = idx >= 0 ? Uri.UnescapeDataString(pair.Substring(idx + 1)) : string.Empty;
                result[key] = value;
            }

            return result;
        }

        public void Dispose() => authStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
