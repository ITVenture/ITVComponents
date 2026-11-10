using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers
{
    /// <summary>
    /// Checks for the external login pages (MVC and Blazor) whether a provider name may be used for a challenge.
    /// </summary>
    public static class ExternalLoginGuard
    {
        /// <summary>
        /// Gets whether <paramref name="provider"/> is a registered remote authentication scheme (OpenId Connect, OAuth,
        /// WS-Federation, ...). Local schemes such as the Identity cookies are rejected, so a provider name taken from a
        /// query string can never challenge them. The scheme does not need to be listed on the login page.
        /// </summary>
        /// <param name="schemes">the scheme provider of the host</param>
        /// <param name="provider">the requested provider name</param>
        /// <returns>true when the scheme exists and is handled by a remote authentication handler</returns>
        public static async Task<bool> IsRemoteSchemeAsync(IAuthenticationSchemeProvider schemes, string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return false;
            }

            var scheme = await schemes.GetSchemeAsync(provider);
            return scheme != null && typeof(IAuthenticationRequestHandler).IsAssignableFrom(scheme.HandlerType);
        }
    }
}
