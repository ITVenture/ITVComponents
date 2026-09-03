using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Globalization;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Pure decision helper for <see cref="TenantUrlGuard"/>'s path-segment mode. Kept separate from the
    /// component so the routing decision is unit-testable without a Blazor renderer.
    /// </summary>
    internal static class TenantUrlGuardLogic
    {
        /// <summary>
        /// Decides whether a navigation target should be rewritten under the current tenant's base path.
        /// Returns the rewritten (tenant-prefixed) URL when the target is an "escape" — an absolute path
        /// that lacks the current tenant prefix AND doesn't address another eligible scope. Returns
        /// <c>null</c> to let the navigation through unchanged: same-prefix paths, paths under
        /// <paramref name="authExclusions"/>, paths under Blazor's <c>/_*</c> infrastructure, and
        /// legitimate cross-tenant switches (first segment is a different eligible scope — the new
        /// circuit will pick up the new base href and resolve cleanly).
        /// <para>
        /// On a host with a culture prefix (<c>UseCulturePath()</c>) the language travels with the
        /// navigation: a target that carries no language inherits the current one - including on a
        /// cross-tenant switch and on the way to an excluded auth path, both of which would otherwise
        /// drop it - and a target that brings its own keeps it, because that is a deliberate language
        /// switch. Without a culture prefix in the base path nothing here behaves differently than before.
        /// </para>
        /// </summary>
        /// <param name="basePath">the current circuit's base path (e.g. <c>/T001/</c> or
        /// <c>/c/de-CH/T001/</c>), or <c>/</c> when neither prefix is active</param>
        /// <param name="target">the absolute target URI of the pending navigation (already filtered to
        /// same-origin by the caller)</param>
        /// <param name="authExclusions">the host-configurable list of path prefixes to leave untouched
        /// (auth endpoints, callbacks, etc.)</param>
        /// <param name="eligibleScopes">scope names the current user is eligible for; used to recognise
        /// cross-tenant switches as legitimate</param>
        /// <returns>the rewritten URL, or <c>null</c> to pass the navigation through unchanged</returns>
        public static string? PlanPathSegmentRewrite(
            string basePath,
            Uri target,
            IList<string> authExclusions,
            ISet<string> eligibleScopes)
        {
            if (string.IsNullOrEmpty(basePath) || basePath == "/")
            {
                return null;
            }

            var absolutePath = target.AbsolutePath;

            // Blazor internals (/_blazor, /_framework, /_content/...) — defensive, normally not seen
            // through NavigationManager but keeps the guard robust if a host wires its own forwarder.
            // Checked before anything else, because these are the one kind of target that must not even
            // gain a language prefix.
            if (absolutePath.StartsWith("/_", StringComparison.Ordinal))
            {
                return null;
            }

            // The language is decided separately from the tenant, and it is decided first: the tenant
            // question below is then asked on the path WITHOUT the language, exactly as it was asked
            // before the culture prefix existed. A target that brings its own language wins - that is a
            // deliberate language switch, and rewriting it under the current one would make switching the
            // language impossible. A target without one inherits the current language, which is what keeps
            // it from getting lost on a cross-tenant switch or on the way to a login page.
            var baseCulture = CulturePath.TryRead(basePath, out _, out var basePrefix) ? basePrefix : string.Empty;
            var tenantBase = baseCulture.Length == 0 ? basePath : basePath.Substring(baseCulture.Length);
            var targetCulture = CulturePath.TryRead(absolutePath, out _, out var targetPrefix) ? targetPrefix : null;
            var culture = targetCulture ?? baseCulture;
            var path = targetCulture == null ? absolutePath : CulturePath.Strip(absolutePath);

            var rewritten = PlanTenantRewrite(tenantBase, path, authExclusions, eligibleScopes);
            if (rewritten == null
                && string.Equals(culture + path, absolutePath, StringComparison.Ordinal))
            {
                // Neither the tenant nor the language changes anything - the navigation is already where
                // it belongs.
                return null;
            }

            return culture + (rewritten ?? path) + target.Query + target.Fragment;
        }

        /// <summary>
        /// The tenant half of the decision, on a path that no longer carries a language prefix. Returns
        /// the path rewritten under <paramref name="basePath"/>, or <c>null</c> when the tenant gives no
        /// reason to touch it - which is not the same as "let the navigation through": the caller may
        /// still have a language to put in front of it.
        /// </summary>
        /// <param name="basePath">the base path without its culture prefix (e.g. <c>/T001/</c>)</param>
        /// <param name="absolutePath">the target path without its culture prefix</param>
        /// <param name="authExclusions">the path prefixes to leave untouched</param>
        /// <param name="eligibleScopes">scope names the current user is eligible for</param>
        /// <returns>the rewritten path, or null when the tenant prefix does not apply</returns>
        private static string? PlanTenantRewrite(
            string basePath,
            string absolutePath,
            IList<string> authExclusions,
            ISet<string> eligibleScopes)
        {
            // A user who is a member of no tenant has a base path of "/" once the language is off it.
            if (string.IsNullOrEmpty(basePath) || basePath == "/")
            {
                return null;
            }

            // Already under the current tenant — nothing to rewrite.
            if (absolutePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Auth/host-excluded paths pass through unchanged.
            for (var i = 0; i < authExclusions.Count; i++)
            {
                var ex = authExclusions[i];
                if (!string.IsNullOrEmpty(ex)
                    && absolutePath.StartsWith(ex, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            // Cross-tenant switch: first path segment is a different eligible scope. Let the navigation
            // proceed so the receiving circuit (after the host's forceLoad / a fresh page load) picks
            // up the new base href and resolves the target tenant cleanly. ScopedPermissionScope still
            // gates ineligible scopes on the receiving side, so this is safe.
            var firstSegment = ExtractFirstSegment(absolutePath);
            if (!string.IsNullOrEmpty(firstSegment) && eligibleScopes.Contains(firstSegment))
            {
                return null;
            }

            // Escape catch: rewrite under the current tenant. Keeps soft navigations that "forgot" the
            // prefix (e.g. NavigateTo("/users") from a tenant-unaware lib) inside the active tenant.
            return basePath + absolutePath.TrimStart('/');
        }

        private static string ExtractFirstSegment(string path)
        {
            var trimmed = path.Trim('/');
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }
            var slash = trimmed.IndexOf('/');
            return slash < 0 ? trimmed : trimmed.Substring(0, slash);
        }
    }
}
