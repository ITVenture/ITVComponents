using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.JWT
{
    /// <summary>
    /// Defines a WebToken-Service that is capable of creating, and refreshing Java-WebTokens
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Creates an access-Token for the specified application key, based on the currently logged-in user
        /// </summary>
        /// <param name="applicationKey"></param>
        /// <returns></returns>
        public string GetJwtToken(string applicationKey);

        /// <summary>
        /// Creates a Refresh-token for the current user
        /// </summary>
        /// <returns>a Refresh-token that can be stored under the current users token-store</returns>
        public string GetRefreshToken();

        /// <summary>
        /// Validates an expired token and returns the user it represents
        /// </summary>
        /// <param name="token">the user-token that is outdated and requires a re-new</param>
        /// <param name="applicationKey">the application key for which this token was issued</param>
        /// <returns>the claims-principal that is represented by the provided user-token</returns>
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token, out string applicationKey);

        /// <summary>
        /// Creates an access-token for the specified identity and application key.
        /// </summary>
        /// <param name="principal">the user for which to create a jwt access-token</param>
        /// <param name="applicationKey">the application for which the access-token is created</param>
        /// <returns>a token that grants access within the requested scope</returns>
        string GetJwtTokenFor(ClaimsPrincipal principal, string applicationKey);
    }
}
