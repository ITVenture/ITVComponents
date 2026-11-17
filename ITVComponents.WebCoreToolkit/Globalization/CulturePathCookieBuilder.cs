using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Globalization
{
    /// <summary>
    /// The cookie builder of an authentication cookie on a host with a culture prefix (<c>/c/{culture}/…</c>):
    /// like the default, the cookie path follows the request's <c>PathBase</c> - but without the culture prefix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>UseCulturePath()</c> runs before <c>UseAuthentication()</c> and moves the prefix into
    /// <c>PathBase</c>. The default builder takes the cookie path from exactly that <c>PathBase</c>, so
    /// signing in under <c>/c/de</c> wrote a cookie that existed only under <c>/c/de</c>, and signing OUT under
    /// <c>/c/de</c> deleted the cookie at <c>Path=/c/de</c> - while the one written at <c>/</c> by a sign-in
    /// without prefix stayed, and the user with it. The language is a property of the reading, not of the
    /// session: the cookie belongs to the application.
    /// </para>
    /// <para>
    /// A path set explicitly on the cookie is used as it is. <c>UseCulturePath()</c> is assumed to be the first
    /// middleware; what other middleware moved into <c>PathBase</c> after it (the shared-asset segment) stays
    /// in the cookie path as before.
    /// </para>
    /// </remarks>
    public sealed class CulturePathCookieBuilder : RequestPathBaseCookieBuilder
    {
        /// <summary>
        /// Initializes a new builder that takes over every setting of <paramref name="template"/>.
        /// </summary>
        /// <param name="template">the builder configured so far</param>
        public CulturePathCookieBuilder(CookieBuilder template)
        {
            // The setter refuses an empty name, and the name is often not there yet: the cookie handler's own
            // post-configuration fills it in later - on whichever builder the options hold by then, this one.
            if (!string.IsNullOrEmpty(template.Name))
            {
                Name = template.Name;
            }

            Path = template.Path;
            Domain = template.Domain;
            HttpOnly = template.HttpOnly;
            SameSite = template.SameSite;
            SecurePolicy = template.SecurePolicy;
            Expiration = template.Expiration;
            MaxAge = template.MaxAge;
            IsEssential = template.IsEssential;
            foreach (var extension in template.Extensions)
            {
                Extensions.Add(extension);
            }
        }

        /// <inheritdoc />
        public override CookieOptions Build(HttpContext context, DateTimeOffset expiresFrom)
        {
            var options = base.Build(context, expiresFrom);
            if (Path == null)
            {
                options.Path = WithoutCulturePrefix(context, options.Path);
            }

            return options;
        }

        /// <summary>
        /// Removes the culture prefix the current request was read with from a cookie path.
        /// </summary>
        /// <param name="context">the current request</param>
        /// <param name="path">a cookie path derived from the request's <c>PathBase</c></param>
        /// <returns>the path without the culture prefix, <c>/</c> when nothing is left; unchanged without a prefix</returns>
        public static string WithoutCulturePrefix(HttpContext context, string path)
        {
            if (string.IsNullOrEmpty(path) || context?.Items[Global.CulturePathPrefixItemKey] is not string prefix)
            {
                return path;
            }

            var outer = context.Items[Global.CulturePathOuterPathBaseItemKey] as string ?? string.Empty;
            var lead = outer + prefix;
            if (!path.StartsWith(lead, StringComparison.OrdinalIgnoreCase)
                || (path.Length > lead.Length && path[lead.Length] != '/'))
            {
                return path;
            }

            var rest = outer + path.Substring(lead.Length);
            return rest.Length == 0 ? "/" : rest;
        }

        /// <summary>
        /// Deletes, on sign-out under a culture prefix, the cookie a sign-in under that prefix wrote before
        /// this builder existed (at <c>Path=/c/{culture}</c>). Without it, such a user would stay signed in
        /// under that prefix until the cookie expires.
        /// </summary>
        /// <param name="context">the sign-out that is under way</param>
        internal void DeleteCookieUnderCulturePrefix(CookieSigningOutContext context)
        {
            if (Path != null || string.IsNullOrEmpty(Name))
            {
                return;
            }

            var asRequested = base.Build(context.HttpContext, DateTimeOffset.UtcNow);
            if (string.Equals(asRequested.Path, context.CookieOptions.Path, StringComparison.Ordinal))
            {
                return;
            }

            context.Options.CookieManager.DeleteCookie(context.HttpContext, Name, new CookieOptions
            {
                Path = asRequested.Path,
                Domain = asRequested.Domain,
                Secure = asRequested.Secure,
                SameSite = asRequested.SameSite,
                HttpOnly = asRequested.HttpOnly,
                IsEssential = true
            });
        }
    }
}
