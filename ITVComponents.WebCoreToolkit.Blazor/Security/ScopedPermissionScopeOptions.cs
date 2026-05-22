using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Options for <see cref="ScopedPermissionScope"/> — the in-memory, route-driven permission-scope strategy
    /// for Blazor. Mirrors the resolution-relevant subset of the cookie-scope options without any cookie
    /// specifics (no cookie name, no encryption).
    /// </summary>
    public class ScopedPermissionScopeOptions
    {
        /// <summary>
        /// Gets or sets the route/query value name that selects the tenant for the current circuit (e.g. "tenant").
        /// The value taken from the route must be one of the user's eligible scopes — the eligibility gate is
        /// enforced server-side by the shared resolution engine. Leave null/empty to disable route-based selection.
        /// </summary>
        public string? RouteOverrideParam { get; set; }

        /// <summary>
        /// Gets or sets the expression that picks a default scope when the route carries none (or an ineligible
        /// one). Defaults to the first eligible scope.
        /// </summary>
        public Func<IContextUserProvider, ScopeInfo[], string> DefaultScopeExpression { get; set; }
            = (_, eligibles) => eligibles.FirstOrDefault()?.ScopeName!;

        /// <summary>
        /// Gets or sets how long (in minutes) a resolved token is trusted before its permissions/features are
        /// re-resolved from the security repository. Defaults to 30, matching the cookie strategy.
        /// </summary>
        public int RenewalMinutes { get; set; } = 30;
    }
}
