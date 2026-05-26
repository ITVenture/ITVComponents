using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Circuit-scoped <see cref="IContextUserProvider"/> for Blazor. Because the DI scope lives for the whole
    /// circuit, one instance == one browser tab — so different tabs naturally carry different ambient state.
    /// The (synchronous) <see cref="User"/> getter is served from a cached principal that is seeded once by
    /// <see cref="ContextUserInitializer"/> and kept current via <see cref="AuthenticationStateProvider"/>'s
    /// change notification. Render-mode-neutral (Server and WebAssembly), no CircuitHandler dependency.
    /// </summary>
    public sealed class BlazorContextUserProvider : IContextUserProvider, IDisposable
    {
        private static readonly ClaimsPrincipal Anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        private readonly AuthenticationStateProvider authStateProvider;
        private readonly NavigationManager navigation;
        private readonly IOptions<ScopedPermissionScopeOptions> scopeOptions;
        private ClaimsPrincipal user = Anonymous;

        public BlazorContextUserProvider(AuthenticationStateProvider authStateProvider, NavigationManager navigation, IServiceProvider services, IOptions<ScopedPermissionScopeOptions> scopeOptions)
        {
            this.authStateProvider = authStateProvider;
            this.navigation = navigation;
            this.scopeOptions = scopeOptions;
            Services = services;
            authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        /// <inheritdoc/>
        public ClaimsPrincipal User => user;

        /// <inheritdoc/>
        public IServiceProvider Services { get; }

        /// <inheritdoc/>
        public IDictionary<string, object> RouteData
        {
            get
            {
                var result = ParseQuery(navigation.Uri);
                var opts = scopeOptions.Value;
                if (opts.TenantSource == TenantSource.PathSegment
                    && !string.IsNullOrEmpty(opts.RouteOverrideParam))
                {
                    var segment = ExtractFirstBaseSegment(navigation.BaseUri);
                    if (!string.IsNullOrEmpty(segment))
                    {
                        result[opts.RouteOverrideParam!] = segment;
                    }
                }
                return result;
            }
        }

        /// <inheritdoc/>
        public string RequestPath
        {
            get
            {
                try { return new Uri(navigation.Uri).AbsolutePath; }
                catch { return null; }
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

        private static string? ExtractFirstBaseSegment(string baseUri)
        {
            try
            {
                var path = new Uri(baseUri).AbsolutePath;
                if (string.IsNullOrEmpty(path)) return null;
                var trimmed = path.Trim('/');
                if (trimmed.Length == 0) return null;
                var slash = trimmed.IndexOf('/');
                return slash < 0 ? trimmed : trimmed.Substring(0, slash);
            }
            catch
            {
                return null;
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
