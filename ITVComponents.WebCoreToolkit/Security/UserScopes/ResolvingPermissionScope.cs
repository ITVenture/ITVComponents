using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.Caching;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.Comparers;
using ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels;
using ITVComponents.WebCoreToolkit.Security.UserScopes.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Permission = ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels.Permission;
using ScopeInfo = ITVComponents.WebCoreToolkit.Security.UserScopes.CookieModels.ScopeInfo;

namespace ITVComponents.WebCoreToolkit.Security.UserScopes
{
    /// <summary>
    /// Shared scope-resolution engine for <see cref="IPermissionScope"/> strategies. It holds the entire
    /// tenant-resolution logic — the eligible-scope gate (the tenant-isolation barrier), permission/feature
    /// resolution, and the <see cref="CookiePermissionRepo"/> push that backs the server-side permission
    /// checks — and reads every piece of ambient state (user, services, route override) through the
    /// host-neutral <see cref="IContextUserProvider"/>. Concrete strategies only supply token persistence
    /// (cookie vs. in-memory) plus their own options; the security-relevant resolution stays here so every
    /// strategy passes through the same gate.
    /// </summary>
    public abstract class ResolvingPermissionScope : PermissionScopeBase
    {
        private readonly IContextUserProvider contextUser;
        private readonly ILogger logger;
        private string currentScope;
        private DateTime lastResolvedUtc;
        private bool preventEndlessLoop = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolvingPermissionScope"/> class.
        /// </summary>
        /// <param name="contextUser">the ambient context (user, services, route) to resolve against</param>
        /// <param name="logger">a logger for diagnostic output of the resolution</param>
        protected ResolvingPermissionScope(IContextUserProvider contextUser, ILogger logger)
        {
            this.contextUser = contextUser;
            this.logger = logger;
        }

        /// <summary>Gets the ambient context (user, services, route) the engine resolves against.</summary>
        protected IContextUserProvider ContextUser => contextUser;

        /// <summary>
        /// Drops the memoized scope so the next <see cref="GetPermissionScopePrefix"/> re-resolves the scope,
        /// which re-selects permissions/features and re-pushes the server-side <see cref="CookiePermissionRepo"/>.
        /// Idempotent against the change-stamp: with an active change-signal it only drops the scope when a
        /// security-relevant entity actually changed since the last resolve. This deduplicates the common fan-out
        /// where many EntityChangeRefresher/SecureView instances on the same circuit each call Refresh() for a
        /// single write — the first re-resolves, the rest become no-ops instead of forcing N redundant
        /// re-selections (and DB reads) on that circuit. Falls back to an unconditional drop when no change-signal
        /// is registered, preserving the previous manual-refresh behaviour.
        /// </summary>
        public override void Refresh()
        {
            // Mirrors the stamp-guard in GetPermissionScopePrefix: re-resolving once per write per circuit is
            // enough, every additional refresher for the same write would read the same already-fresh data.
            var signal = contextUser.Services.GetService<IEntityChangeSignal>();
            if (signal == null || signal.GetLastChange(EntityChangeTopics.Security) > lastResolvedUtc)
            {
                currentScope = null;
            }
        }

        /// <summary>
        /// Gets the route-value name that, when present, overrides the stored scope (subject to the eligibility
        /// gate). Return null/empty to disable route-based overriding for this strategy.
        /// </summary>
        protected abstract string RouteOverrideParam { get; }

        /// <summary>
        /// Gets the expression that picks a default scope when none is stored or the stored one is no longer
        /// eligible for the current user.
        /// </summary>
        protected abstract Func<IContextUserProvider, Models.ScopeInfo[], string> DefaultScopeExpression { get; }

        /// <summary>
        /// Loads the persisted token for the current ambient context, or null when none exists. Persistence
        /// (cookie, in-memory, …) is the strategy's only concern; eligibility/permission resolution is shared.
        /// </summary>
        /// <returns>the stored <see cref="UserScope"/> token, or null</returns>
        protected abstract UserScope LoadStoredToken();

        /// <summary>
        /// Indicates whether a loaded token is too old to trust and must be rebuilt from scratch.
        /// </summary>
        /// <param name="token">the token returned by <see cref="LoadStoredToken"/></param>
        /// <returns>true when the token should be discarded and rebuilt</returns>
        protected abstract bool IsTokenStale(UserScope token);

        /// <summary>
        /// Persists the token after the engine refreshed its permissions/features or changed its scope.
        /// </summary>
        /// <param name="token">the token to persist</param>
        protected abstract void PersistToken(UserScope token);

        /// <summary>
        /// Sets the permissionScope to a new value
        /// </summary>
        /// <param name="newScope">the new scope to apply for the current user</param>
        /// <param name="asTemporary">whether the scope is applied as a temporary, component-local override</param>
        protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
        {
            if (!asTemporary)
            {
                var token = ReadScopeToken(out var createdNew, out var secc);
                UpdateToken(newScope, token, secc, createdNew, true, false);
            }
            else
            {
                SetFixedScope(newScope);
            }
        }

        /// <summary>
        /// Enables a derived class to provide a permissionScopePrefix for the case that it is not being bypassed internally
        /// </summary>
        /// <returns>a string representing the current permission scope</returns>
        protected override string GetPermissionScopePrefix()
        {
            if (ImpersonationExplicitDeactivated)
            {
                return GetCurrentScope();
            }

            // A security-relevant write since we last resolved must drop the memoized scope — otherwise a
            // long-lived scope (the Blazor circuit keeps one IPermissionScope for its whole lifetime) keeps
            // serving stale permissions/features until something external calls Refresh(). The check is a cheap
            // tracker-timestamp lookup (no DB), and busting the memo here means the re-resolution runs INLINE on
            // the caller's render/request thread — avoiding the off-thread re-query (via EntityChangeRefresher's
            // InvokeAsync) that races the scoped DbContext and surfaces as "a second operation was started…".
            if (currentScope != null && SecurityChangedSinceResolve())
            {
                currentScope = null;
            }

            if (currentScope == null)
            {
                var stampUtc = DateTime.UtcNow;
                currentScope = GetCurrentScope();
                lastResolvedUtc = stampUtc;
            }

            return currentScope;
        }

        /// <summary>
        /// Cheap, DB-free check whether a security-relevant entity changed since the memoized scope was last
        /// resolved. Returns false when no change-signal is registered (EntityWriteTracker inactive), preserving
        /// the previous memoize-until-Refresh behaviour.
        /// </summary>
        private bool SecurityChangedSinceResolve()
        {
            var signal = contextUser.Services.GetService<IEntityChangeSignal>();
            if (signal == null)
            {
                return false;
            }

            return signal.GetLastChange(EntityChangeTopics.Security) > lastResolvedUtc;
        }

        /// <summary>
        /// Gets the current scope from the configured claims/eligible scopes of the signed-in user. Running as a
        /// side effect, this is where the eligible-scope gate is enforced and the resolved permissions are pushed.
        /// </summary>
        /// <returns>the current scope for the logged-in user</returns>
        private string GetCurrentScope()
        {
            if (preventEndlessLoop)
            {
                return currentScope;
            }

            preventEndlessLoop = true;
            try
            {
                var user = contextUser.User;
                var identities = user?.Identities?.ToArray();
                if (identities != null && identities.Any(n => n.IsAuthenticated))
                {
                    if (user.HasClaim(c => c.Type == ClaimTypes.FixedUserScope))
                    {
                        var fixedUserScope = user.Claims
                            .First(n => n.Type == ClaimTypes.FixedUserScope).Value;
                        IsScopeExplicit = true;
                        return fixedUserScope;
                    }
                }

                var scopeToken = ReadScopeToken(out var isNew, out var secc);
                string retVal = scopeToken.ScopeName;
                if (scopeToken.UserLabels.Length == 0)
                {
                    // No labels is the normal state of an anonymous request and stays quiet. If somebody IS
                    // signed in and there is still no label, the IUserNameMapper yielded nothing for that
                    // identity — a misconfiguration that locks the user out of every tenant and would
                    // otherwise leave no trace whatsoever, because this returns the same null either way.
                    if (identities != null && identities.Any(n => n.IsAuthenticated))
                    {
                        // Name the mapper and the identity's Name: with SimpleUserNameMapper the label IS
                        // IIdentity.Name, so an empty label means that name is empty — which for a
                        // ClaimsIdentity means its NameClaimType names a claim the identity does not carry.
                        // That is a configuration detail nobody would guess from a missing tenant.
                        var mapper = contextUser.Services.GetService<IUserNameMapper>();
                        logger.LogWarning(
                            "A signed-in user produced no user labels. {Mapper} returned nothing for the authenticated identities (authentication types: {AuthenticationTypes}; identity names: {IdentityNames}), so no scope can be resolved.",
                            mapper?.GetType().Name ?? "No IUserNameMapper",
                            string.Join(", ", identities.Where(n => n.IsAuthenticated)
                                .Select(n => n.AuthenticationType ?? "<none>")),
                            string.Join(", ", identities.Where(n => n.IsAuthenticated)
                                .Select(n => string.IsNullOrEmpty(n.Name) ? "<empty>" : n.Name)));
                    }

                    return null;
                }

                var routeData = contextUser.RouteData;
                if (!string.IsNullOrEmpty(RouteOverrideParam) &&
                    routeData != null &&
                    routeData.TryGetValue(RouteOverrideParam, out var ovRaw) &&
                    ovRaw is string ov && !string.IsNullOrEmpty(ov))
                {
                    // The route may only override to a scope the user is actually eligible for. This check is
                    // the tenant-isolation gate and is intentionally identical for every strategy.
                    //
                    // What travels on from here is the ELIGIBLE scope's own spelling, not the one the route
                    // happened to use. The gate is deliberately case-insensitive, but everything downstream
                    // compares the scope name as a plain value — above all the tree functions in the
                    // database, and PostgreSQL compares exactly. A route saying "t001" for a tenant stored
                    // as "T001" would otherwise pass the gate here and resolve to nothing at all further
                    // down, which reads like a permission problem and is none.
                    var routeScope = scopeToken.EligibleScopes.FirstOrDefault(n =>
                        n.ScopeName.Equals(ov, StringComparison.OrdinalIgnoreCase));
                    if (routeScope != null)
                    {
                        UpdateToken(routeScope.ScopeName, scopeToken, secc, isNew, false, true);
                        IsScopeExplicit = true;
                        return routeScope.ScopeName;
                    }
                }

                IsScopeExplicit = false;
                // Case-insensitively, like the route gate above and like every lookup in UserScope. This
                // comparison used to be the one case-SENSITIVE answer to a question the same request
                // answers three times; on PostgreSQL, where names keep their spelling reliably, that
                // difference is the one that shows.
                var currentEligible = scopeToken.EligibleScopes.FirstOrDefault(n =>
                    n.ScopeName.Equals(retVal, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(retVal) ||
                    currentEligible == null)
                {
                    retVal = DefaultScopeExpression(contextUser, scopeToken.EligibleScopes);
                    logger.LogDebug($"Default-Value of current scope: {retVal}");

                    // DefaultScopeExpression is a HOST delegate — nothing about it guarantees that what it
                    // yields is one of the eligible scopes. The common implementation reads a claim (a
                    // "default tenant"), while eligibility comes from the database; the two disagree as soon
                    // as the claim outlives the membership, or as soon as the eligible set comes back empty
                    // for an unrelated reason. Unchecked, that mismatch did not surface here at all: it
                    // travelled on into UserScope.First and came out as "Sequence contains no matching
                    // element", naming neither the scope asked for nor the ones on offer, on whichever
                    // component happened to ask first.
                    currentEligible = scopeToken.EligibleScopes.FirstOrDefault(n =>
                        n.ScopeName.Equals(retVal, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(retVal) && currentEligible == null)
                    {
                        currentEligible = scopeToken.EligibleScopes.FirstOrDefault();
                        logger.LogWarning(
                            "The default scope '{RequestedScope}' is not among the eligible scopes ({EligibleScopes}). Falling back to '{FallbackScope}'.",
                            retVal,
                            scopeToken.EligibleScopes.Length != 0
                                ? string.Join(", ", scopeToken.EligibleScopes.Select(n => n.ScopeName))
                                : "none",
                            currentEligible?.ScopeName ?? "<none>");
                    }
                }

                // Either way the canonical spelling wins from here on.
                retVal = currentEligible?.ScopeName;

                // A user who is a member of no tenant has no eligible scope to resolve to (the default
                // expression yields null/empty over an empty EligibleScopes set). There is nothing to push to
                // the permission repo, and UpdateScopePermissions/-Features assume the scope is one of the
                // eligible scopes (EligibleScopes.First) — so calling UpdateToken here would throw. Returning
                // null leaves the user with an empty permission set, locked out of everything except the
                // tenant-neutral identity pages, which is the intended behaviour for a tenant-less account.
                if (string.IsNullOrEmpty(retVal))
                {
                    return null;
                }

                UpdateToken(retVal, scopeToken, secc, isNew, true, true);
                return retVal;
            }
            finally
            {
                preventEndlessLoop = false;
            }
        }

        private void UpdateToken(string scope, UserScope scopeToken, ISecurityRepository secc, bool forceRefresh, bool setAsDefault, bool pushRepo)
        {
            bool setRefreshed = false;
            // A security-relevant write (permissions, role-permissions, global roles, tenant-users, …) since the
            // scope was last refreshed invalidates the cached permissions/features, even within the TTL window.
            bool securityChanged = SecurityChangedSince(scope, scopeToken);

            // A security-relevant change invalidates the cached permission/feature snapshot. Drop a stale
            // CookiePermissionRepo from the top of the repo-stack BEFORE re-reading: that way the rebuild below
            // pulls fresh permissions, features AND known-permissions from the root (DB) repo — GetFeatures /
            // GetKnownPermissions read through Current, which was still the stale snapshot — and the push below
            // actually replaces the snapshot instead of being skipped (its guard requires Current to NOT be a
            // CookiePermissionRepo, so without this pop a permission/feature change never reached the snapshot
            // that the authorization checks read, while only the in-token copy was refreshed).
            if (pushRepo && (securityChanged || forceRefresh) &&
                secc is SecurityRepository { Current: CookiePermissionRepo } stale)
            {
                stale.PopRepo();
            }

            var perms = scopeToken.GetPermissionsOf(scope);
            if (perms == null || forceRefresh || securityChanged)
            {
                perms = (from t in scopeToken.UserLabels
                        select secc.GetPermissions(t.UserLabels, scope, t.AuthenticationType))
                    .SelectMany(n => n.Select(n => n.PermissionName)).Distinct().ToArray();
                scopeToken.UpdateScopePermissions(scope, secc.GetKnownPermissions(scope), perms);
                setRefreshed = true;
            }

            var features = scopeToken.GetFeaturesOf(scope);
            if (features == null || forceRefresh || securityChanged)
            {
                var tmp = secc.GetFeatures(scope).ToArray();
                features = scopeToken.UpdateScopeFeatures(scope, tmp);
                setRefreshed = true;
            }

            if (setRefreshed)
            {
                scopeToken.SetScopeRefreshed(scope);
            }

            if (pushRepo && secc is SecurityRepository { Current: not CookiePermissionRepo } wrap)
            {
                wrap.PushRepo(new CookiePermissionRepo(scopeToken.UserLabels, scopeToken.EligibleScopes, scope, perms, scopeToken.KnownPermissions, features, wrap.Current));
            }

            if (setAsDefault)
            {
                setRefreshed |= scopeToken.ScopeName != scope;
                scopeToken.ScopeName = scope;
            }

            if (setRefreshed)
            {
                PersistToken(scopeToken);
            }
        }

        private UserScope ReadScopeToken(out bool createdNew, out ISecurityRepository secc)
        {
            createdNew = false;
            var scopeToken = LoadStoredToken();

            if (scopeToken == null || IsTokenStale(scopeToken))
            {
                scopeToken = new UserScope { ScopeName = scopeToken?.ScopeName };
                createdNew = true;
            }

            ScopeInfo[] eligibles = null;
            secc = null;
            var lbl = GetUserLabels();
            if (!createdNew && scopeToken.UserLabels != null && UserValidateHelper.IsUserOk(scopeToken.UserLabels, lbl))
            {
                eligibles = scopeToken.EligibleScopes;
                secc = GetSecurityRepo();
            }

            // Empty is not the same as "resolved, nothing to do" — it is a lookup that came back with
            // nothing, and asking again is the only way to tell the two apart. Treating a non-null but
            // EMPTY set as an answer meant it stayed put until the token expired (RenewalMinutes), so a
            // single failed lookup locked the user out of every tenant for that whole window and repeated
            // the same unhelpful error instead of recovering. A user who really is in no tenant pays for
            // this with one query per resolution — which is the rare case, and the honest one.
            if (eligibles is not { Length: > 0 })
            {
                scopeToken.UserLabels = lbl;
                eligibles = GetEligibleScopes(out secc, lbl);
                scopeToken.EligibleScopes = eligibles;

                // Only worth a word when there was somebody to look up. GetUserLabels reports
                // AUTHENTICATED identities only, so no labels means nobody is signed in — the ordinary
                // anonymous request, which every page serves before a login and which is not a lookup that
                // failed. Warning on those buries the case that does deserve attention: a signed-in user
                // for whom the database returns no tenant at all.
                var labels = lbl.SelectMany(n => n.UserLabels).ToArray();
                if (eligibles is not { Length: > 0 } && labels.Length != 0)
                {
                    logger.LogWarning(
                        "No eligible scopes for user labels [{UserLabels}]. The user cannot switch to any tenant; only tenant-neutral pages remain reachable.",
                        string.Join(", ", labels));
                }
            }
            return scopeToken;
        }

        private ScopeInfo[] GetEligibleScopes(out ISecurityRepository securityRepo, AuthTypeUserLabels[] userLabels)
        {
            var scc = securityRepo = GetSecurityRepo();
            return (from t in userLabels
                    select
                        scc.GetEligibleScopes(t.UserLabels, t.AuthenticationType))
                .SelectMany(n => n).Distinct(new ScopeInfoComparer())
                .Select(n => new ScopeInfo
                {
                    AccessMode = n.AccessMode,
                    Created = DateTime.Now,
                    ScopeDisplayName = n.ScopeDisplayName,
                    ScopeName = n.ScopeName
                })
                .ToArray();
        }

        private ISecurityRepository GetSecurityRepo()
        {
            return contextUser.Services.GetService<ISecurityRepository>();
        }

        /// <summary>
        /// Determines whether a security-relevant entity changed since the given scope was last refreshed.
        /// Returns false when no change-signal is registered (EntityWriteTracker inactive) — preserving the
        /// previous TTL-only behaviour.
        /// </summary>
        private bool SecurityChangedSince(string scope, UserScope scopeToken)
        {
            if (string.IsNullOrEmpty(scope) || scopeToken?.EligibleScopes == null)
            {
                return false;
            }

            var signal = contextUser.Services.GetService<IEntityChangeSignal>();
            if (signal == null)
            {
                return false;
            }

            var sc = scopeToken.EligibleScopes.FirstOrDefault(n =>
                n.ScopeName.Equals(scope, StringComparison.OrdinalIgnoreCase));
            // sc.Created is local time (set via SetScopeRefreshed); the signal reports UTC.
            return sc != null && signal.GetLastChange(EntityChangeTopics.Security).ToLocalTime() > sc.Created;
        }

        private AuthTypeUserLabels[] GetUserLabels()
        {
            var userProvider = contextUser.Services.GetRequiredService<IUserNameMapper>();
            return (from t in contextUser.User.Identities
                    where t.IsAuthenticated
                    select new { UserLabels = userProvider.GetUserLabels(t), AuthType = t.AuthenticationType })
                .GroupBy(m => m.AuthType)
                .Select(n => new AuthTypeUserLabels { UserLabels = n.SelectMany(n => n.UserLabels).Distinct().ToArray(), AuthenticationType = n.Key }).ToArray();
        }
    }
}
