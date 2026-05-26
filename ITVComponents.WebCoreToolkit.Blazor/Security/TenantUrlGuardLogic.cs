using System;
using System.Collections.Generic;

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
        /// </summary>
        /// <param name="basePath">the current circuit's base path (e.g. <c>/T001/</c>), or <c>/</c> when
        /// no tenant prefix is active</param>
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

            // Blazor internals (/_blazor, /_framework, /_content/...) — defensive, normally not seen
            // through NavigationManager but keeps the guard robust if a host wires its own forwarder.
            if (absolutePath.StartsWith("/_", StringComparison.Ordinal))
            {
                return null;
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
            return basePath + absolutePath.TrimStart('/') + target.Query + target.Fragment;
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
