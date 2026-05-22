using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

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
        private ClaimsPrincipal user = Anonymous;

        public BlazorContextUserProvider(AuthenticationStateProvider authStateProvider, NavigationManager navigation, IServiceProvider services)
        {
            this.authStateProvider = authStateProvider;
            this.navigation = navigation;
            Services = services;
            authStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        /// <inheritdoc/>
        public ClaimsPrincipal User => user;

        /// <inheritdoc/>
        public IServiceProvider Services { get; }

        /// <inheritdoc/>
        public IDictionary<string, object> RouteData => ParseQuery(navigation.Uri);

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
