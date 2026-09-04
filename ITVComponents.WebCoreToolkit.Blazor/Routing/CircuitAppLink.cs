using System;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Routing.Impl;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.Routing
{
    /// <summary>
    /// <see cref="IAppLink"/> for a Blazor host. Inside a circuit every link is emitted RELATIVE, because the
    /// <c>&lt;base href&gt;</c> that <c>TenantBaseHref</c> writes already carries the whole prefix - culture,
    /// shared asset, tenant - and a relative link therefore inherits all of it without naming any of it.
    /// <para>
    /// That is not a micro-optimization, it is the difference between a link that works and one that quietly
    /// drops half the context: a root-absolute href under <c>&lt;base href="/c/de-CH/T001/"&gt;</c> resolves
    /// OUTSIDE the base-URI space, so Blazor's JS does not intercept the click, the browser performs a plain
    /// document load, and everything that lived in <c>PathBase</c> is gone from the request. The tenant used
    /// to be prepended by hand to survive that; the language had nobody doing it for it and was lost on every
    /// menu click - while the same target reached through <c>NavigateTo</c> kept it, because
    /// <c>TenantUrlGuard</c> sees in-circuit navigations and the base href does the rest.
    /// </para>
    /// <para>
    /// Outside a circuit - the host's Identity pages and any other non-Blazor endpoint, which render without
    /// a base href - the inherited <see cref="HttpAppLink"/> behaviour applies unchanged. The decision is
    /// made per call rather than per registration, because both kinds of endpoint live in the same
    /// application and the same service scope.
    /// </para>
    /// </summary>
    public sealed class CircuitAppLink : HttpAppLink
    {
        private readonly NavigationManager navigation;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitAppLink"/> class.
        /// </summary>
        /// <param name="userProvider">the ambient context</param>
        /// <param name="permissionScope">the current scope</param>
        /// <param name="navigation">the circuit's navigation manager; uninitialized outside a circuit</param>
        public CircuitAppLink(IContextUserProvider userProvider, IPermissionScope permissionScope,
            NavigationManager navigation)
            : base(userProvider, permissionScope)
        {
            this.navigation = navigation;
        }

        /// <inheritdoc/>
        public override string Resolve(string moduleUrl)
        {
            if (!TryGetBasePath(out var basePath))
            {
                return base.Resolve(moduleUrl);
            }

            var path = AppLinkPath.Normalize(moduleUrl);
            if (path.Length == 0)
            {
                return string.Empty;
            }

            // The application root has no relative spelling: an empty href resolves to the current document
            // rather than to the base, and "./" to the current directory. The base path itself is the right
            // answer, and it is root-absolute without being an escape - it IS the base-URI space, so Blazor
            // still intercepts the click.
            return path == "/" ? basePath : path.TrimStart('/');
        }

        /// <inheritdoc/>
        public override string CurrentModuleUrl
        {
            get
            {
                if (!TryGetBasePath(out var basePath))
                {
                    return base.CurrentModuleUrl;
                }

                string absolute;
                try
                {
                    absolute = new Uri(navigation.Uri).AbsolutePath;
                }
                catch (Exception ex) when (ex is UriFormatException or InvalidOperationException)
                {
                    return base.CurrentModuleUrl;
                }

                var rest = absolute.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
                    ? absolute.Substring(basePath.Length - 1)
                    : absolute;

                // On the auth-excluded paths TenantBaseHref falls back to "/" while the address may still
                // carry a language, so the leftovers get the same treatment as anywhere else.
                return AppLinkPath.StripPrefixes(rest, Tenant);
            }
        }

        /// <inheritdoc/>
        public override string CulturePrefix
        {
            get
            {
                if (!TryGetBasePath(out var basePath))
                {
                    return base.CulturePrefix;
                }

                return CulturePath.TryRead(basePath, out _, out var prefix) ? prefix : string.Empty;
            }
        }

        /// <summary>
        /// The circuit's base path with a leading and a trailing slash, or false when there is no circuit -
        /// which is what <see cref="NavigationManager"/> reports by throwing on an uninitialized instance.
        /// </summary>
        /// <param name="basePath">the base path, e.g. <c>/c/de-CH/T001/</c></param>
        /// <returns>true when a circuit (or a prerender) is at hand</returns>
        private bool TryGetBasePath(out string basePath)
        {
            try
            {
                basePath = new Uri(navigation.BaseUri).AbsolutePath;
                if (string.IsNullOrEmpty(basePath))
                {
                    basePath = "/";
                }

                return true;
            }
            catch (Exception ex) when (ex is UriFormatException or InvalidOperationException)
            {
                basePath = "/";
                return false;
            }
        }
    }
}
