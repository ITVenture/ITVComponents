using System;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.UserScopes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScopeInfo = ITVComponents.WebCoreToolkit.Models.ScopeInfo;
using UserScope = ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels.UserScope;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Blazor <see cref="IPermissionScope"/> strategy: keeps the resolved <see cref="UserScope"/> token in
    /// memory for the lifetime of the (circuit-scoped) instance, so each browser tab carries its own tenant
    /// without any browser-wide cookie. The tenant itself is taken from the route/query and validated against
    /// the user's eligible scopes by the shared <see cref="ResolvingPermissionScope"/> engine — the
    /// tenant-isolation gate stays server-side and identical to the cookie strategy.
    /// <para>
    /// Switching tenant inside a single tab is performed by navigating (forceLoad) to the tenant-carrying URL:
    /// that rebuilds the circuit and, with it, this scope and its in-memory token, yielding a clean resolution.
    /// </para>
    /// </summary>
    public sealed class ScopedPermissionScope : ResolvingPermissionScope
    {
        private readonly IOptions<ScopedPermissionScopeOptions> options;
        private UserScope? token;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopedPermissionScope"/> class.
        /// </summary>
        /// <param name="contextUser">the circuit-scoped ambient context (user, services, route)</param>
        /// <param name="options">the scoped-permission-scope options</param>
        /// <param name="logger">a logger for diagnostic output</param>
        public ScopedPermissionScope(IContextUserProvider contextUser, IOptions<ScopedPermissionScopeOptions> options, ILogger<ScopedPermissionScope> logger)
            : base(contextUser, logger)
        {
            this.options = options;
        }

        /// <inheritdoc/>
        // null is a valid value (disables route override); the engine guards it via string.IsNullOrEmpty.
        protected override string RouteOverrideParam => options.Value.RouteOverrideParam!;

        /// <inheritdoc/>
        protected override Func<IContextUserProvider, ScopeInfo[], string> DefaultScopeExpression => options.Value.DefaultScopeExpression;

        /// <inheritdoc/>
        // null on first access (no token yet); the engine treats null as "create a fresh token".
        protected override UserScope LoadStoredToken() => token!;

        /// <inheritdoc/>
        protected override bool IsTokenStale(UserScope t) => DateTime.Now.Subtract(t.Created).TotalMinutes > options.Value.RenewalMinutes;

        /// <inheritdoc/>
        protected override void PersistToken(UserScope t) => token = t;
    }
}
