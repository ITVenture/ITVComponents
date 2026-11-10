using System;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Components.Account
{
    /// <summary>
    /// Keeps return urls of the account pages inside the application, like <c>LocalRedirect</c> on the MVC side.
    /// </summary>
    internal static class LocalReturnUrl
    {
        /// <summary>
        /// Returns <paramref name="returnUrl"/> when it points into this application, otherwise "/".
        /// Accepted are app-relative paths ("/x", "~/x") and absolute urls below the base uri of the application.
        /// </summary>
        /// <param name="returnUrl">the requested return url</param>
        /// <param name="nav">the navigation manager of the page</param>
        /// <param name="logger">receives a warning when a url is rejected</param>
        public static string Sanitize(string? returnUrl, NavigationManager nav, ILogger logger)
        {
            if (string.IsNullOrEmpty(returnUrl))
            {
                return "/";
            }

            if (returnUrl[0] == '/')
            {
                // "//host" and "/\host" are protocol-relative urls to a foreign host.
                if (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
                {
                    return returnUrl;
                }
            }
            else if (returnUrl.StartsWith("~/", StringComparison.Ordinal))
            {
                return Sanitize(returnUrl.Substring(1), nav, logger);
            }
            else if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
                     && absolute.AbsoluteUri.StartsWith(nav.BaseUri, StringComparison.OrdinalIgnoreCase))
            {
                return absolute.AbsoluteUri;
            }

            logger.LogWarning("Rejected non-local return url '{ReturnUrl}'; redirecting to the start page instead.", returnUrl);
            return "/";
        }
    }
}
