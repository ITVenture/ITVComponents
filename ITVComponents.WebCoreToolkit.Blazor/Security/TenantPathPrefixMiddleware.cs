using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Blazor.Security
{
    /// <summary>
    /// Validates the first URL path segment in <see cref="TenantSource.PathSegment"/> mode against the
    /// authenticated user's eligible scopes BEFORE Blazor renders. Eliminates the need for hosts to write
    /// their own tenant-segment validation: an unknown or non-eligible segment yields a 404; the root
    /// ("/") redirects to the default eligible scope; auth/static/Blazor-internal paths are skipped; the
    /// validated segment is stashed in <see cref="HttpContext.Items"/> under <see cref="TenantSegmentItemKey"/>
    /// so <see cref="TenantBaseHref"/> can emit the matching <c>&lt;base href&gt;</c>.
    /// <para>
    /// Configured entirely through <see cref="ScopedPermissionScopeOptions"/>: <c>RouteOverrideParam</c>,
    /// <c>TenantSource</c>, <c>AuthPathExclusions</c>. No-op when <c>TenantSource</c> is not
    /// <see cref="TenantSource.PathSegment"/>.
    /// </para>
    /// </summary>
    public sealed class TenantPathPrefixMiddleware
    {
        /// <summary>
        /// Key under which the validated tenant segment is stored in <see cref="HttpContext.Items"/>.
        /// </summary>
        public const string TenantSegmentItemKey = "ITVComponents.WebCoreToolkit.Blazor.TenantSegment";

        private readonly RequestDelegate next;
        private readonly IOptions<ScopedPermissionScopeOptions> options;
        private readonly ILogger<TenantPathPrefixMiddleware> logger;

        public TenantPathPrefixMiddleware(RequestDelegate next, IOptions<ScopedPermissionScopeOptions> options, ILogger<TenantPathPrefixMiddleware> logger)
        {
            this.next = next;
            this.options = options;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var opts = options.Value;
            if (opts.TenantSource != TenantSource.PathSegment
                || string.IsNullOrEmpty(opts.RouteOverrideParam))
            {
                await next(context);
                return;
            }

            var path = context.Request.Path.Value ?? "/";

            // Skip Blazor internals (/_blazor, /_framework, /_content/...) and host-configured exclusions
            // (auth endpoints, callback URLs, etc.). Static files served from conventional paths are also
            // skipped: their first segment ("css", "js", "lib", "img") will never match an eligible scope,
            // so they would 404 anyway — listing them explicitly keeps log output cleaner.
            if (path.StartsWith("/_", StringComparison.Ordinal)
                || IsExcluded(path, opts.AuthPathExclusions))
            {
                await next(context);
                return;
            }

            var user = context.User;
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                // Let downstream [Authorize] / authentication challenge decide; the segment will be
                // re-validated after sign-in when the user lands here again.
                await next(context);
                return;
            }

            var mapper = context.RequestServices.GetService<IUserNameMapper>();
            var repo = context.RequestServices.GetService<ISecurityRepository>();
            if (mapper == null || repo == null)
            {
                logger.LogWarning("TenantPathPrefix: skipping validation; IUserNameMapper or ISecurityRepository not in DI.");
                await next(context);
                return;
            }

            var eligible = ResolveEligibleScopes(user, mapper, repo);
            if (eligible.Length == 0)
            {
                // An authenticated user who is a member of no tenant has no tenant segment to validate or strip.
                // Don't lock them out here: let the request flow through to the tenant-neutral pages that only
                // require a signed-in user (Home, account management, …). The scope engine resolves them to a
                // null scope, so any page that genuinely needs an active tenant enforces that itself — its
                // permission/feature checks (SecureView, [Authorize] policies, …) fail against the null scope
                // and that module responds 403/redirect. Keeping the middleware transparent here makes
                // tenant-gating a per-module concern instead of an all-or-nothing gate at the front door.
                logger.LogDebug("TenantPathPrefix: authenticated user has no eligible scopes; passing {Path} through untouched", path);
                await next(context);
                return;
            }

            var firstSegment = ExtractFirstSegment(path);
            if (string.IsNullOrEmpty(firstSegment))
            {
                // URL is "/" → redirect to default eligible scope so the rest of the app boots inside a
                // valid tenant context. First eligible is the v1 choice; richer default-selection can be
                // layered on later (e.g. last-used-tenant from a user-property store).
                var defaultScope = eligible[0].ScopeName;
                context.Response.Redirect($"/{defaultScope}/", permanent: false);
                return;
            }

            if (!eligible.Any(s => string.Equals(s.ScopeName, firstSegment, StringComparison.Ordinal)))
            {
                // Same response whether the tenant doesn't exist or the user just isn't eligible — no
                // information leak about which tenants exist. The *log* may say more, though: see below.
                LogRejectedSegment(context, path, firstSegment, eligible);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Items[TenantSegmentItemKey] = firstSegment;

            // Strip the tenant segment from Request.Path and append it to Request.PathBase — same
            // shape as app.UsePathBase, but per-tenant. Without the strip, endpoint routing would
            // look for @page "/{tenant}/foo" instead of @page "/foo", and every page 404'd. With the
            // PathBase set, Blazor's NavigationManager / link-generation stay tenant-aware downstream
            // (relative redirects automatically carry the prefix).
            var prefix = "/" + firstSegment;
            context.Request.PathBase = context.Request.PathBase.Add(new PathString(prefix));
            context.Request.Path = path.Length > prefix.Length
                ? new PathString(path.Substring(prefix.Length))
                : new PathString("/");

            await next(context);
        }

        /// <summary>
        /// Resolves the union of eligible scopes for every authenticated identity on the given principal.
        /// Shared with <see cref="TenantUrlGuard"/> so the same set drives both server-side path validation
        /// and client-side cross-tenant-switch detection.
        /// </summary>
        internal static ScopeInfo[] ResolveEligibleScopes(System.Security.Claims.ClaimsPrincipal user, IUserNameMapper mapper, ISecurityRepository repo)
        {
            var perAuth = (from id in user.Identities
                           where id.IsAuthenticated
                           select new { Labels = mapper.GetUserLabels(id), AuthType = id.AuthenticationType ?? string.Empty })
                .GroupBy(m => m.AuthType)
                .Select(g => new AuthTypeUserLabels
                {
                    UserLabels = g.SelectMany(x => x.Labels).Distinct().ToArray(),
                    AuthenticationType = g.Key
                }).ToArray();

            return perAuth
                .SelectMany(t => repo.GetEligibleScopes(t.UserLabels, t.AuthenticationType))
                .GroupBy(s => s.ScopeName, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToArray();
        }

        /// <summary>
        /// Explains the 404 in the log. Two cases have to be told apart, because they cost very different
        /// amounts of time to diagnose: a genuinely foreign/mistyped tenant segment (expected, Information),
        /// and a request that came <em>from a page inside one of the user's own tenants</em> — that one is
        /// almost always a root-absolute link emitted by a component, which the browser resolves outside the
        /// base-URI space and therefore never routes through the circuit, so <see cref="TenantUrlGuard"/>
        /// cannot rewrite it. The response is identical in both cases (no information leak); only the log
        /// entry differs, and it names the probable cause so the next occurrence is a one-line diagnosis
        /// instead of a hunt through the host wiring.
        /// </summary>
        private void LogRejectedSegment(HttpContext context, string path, string firstSegment, ScopeInfo[] eligible)
        {
            var referer = context.Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer)
                && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri)
                && string.Equals(refererUri.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                var refererSegment = ExtractFirstSegment(refererUri.AbsolutePath);
                if (!string.IsNullOrEmpty(refererSegment)
                    && eligible.Any(s => string.Equals(s.ScopeName, refererSegment, StringComparison.Ordinal)))
                {
                    logger.LogWarning(
                        "TenantPathPrefix: responding 404 for {Path} — first segment '{Segment}' is not one of the user's eligible scopes, but the referring page ({Referer}) was inside the eligible tenant '{RefererScope}'. This is almost always a root-absolute link or navigation emitted by a component: it resolves outside the base-URI space, the browser therefore leaves the Blazor circuit and TenantUrlGuard never sees it. Emit view targets relative (without a leading slash).",
                        path, firstSegment, referer, refererSegment);
                    return;
                }
            }

            logger.LogInformation("TenantPathPrefix: segment '{Segment}' not in user's eligible scopes; responding 404 for {Path}", firstSegment, path);
        }

        private static string ExtractFirstSegment(string path)
        {
            var trimmed = path.Trim('/');
            if (trimmed.Length == 0) return string.Empty;
            var slash = trimmed.IndexOf('/');
            return slash < 0 ? trimmed : trimmed.Substring(0, slash);
        }

        private static bool IsExcluded(string path, IList<string> exclusions)
        {
            for (var i = 0; i < exclusions.Count; i++)
            {
                if (!string.IsNullOrEmpty(exclusions[i])
                    && path.StartsWith(exclusions[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
