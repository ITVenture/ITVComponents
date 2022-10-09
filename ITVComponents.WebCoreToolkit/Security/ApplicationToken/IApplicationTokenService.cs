using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.ApplicationToken
{
    /// <summary>
    /// Defines a service that is capable of validating and refreshing application-tokens for specific users
    /// </summary>
    public interface IApplicationTokenService
    {
        /// <summary>
        /// Gets the ApplicationUser label for the specified user
        /// </summary>
        /// <param name="principal">the user for which to get the app-user label.</param>
        /// <param name="applicationKey">the app-key of the requesting application</param>
        /// <returns>the app-user label for the given user</returns>
        string GetApplicationUserLabel(IPrincipal principal, string applicationKey);

        /// <summary>
        /// Checks for the given user and application key, if the provided refresh-token is valid
        /// </summary>
        /// <param name="principal">the user, that is logged in or queries a new access-token</param>
        /// <param name="applicationKey">the key of the application that is trying to log in as the given user</param>
        /// <param name="refreshToken">the provided refresh-token</param>
        /// <returns>a value indicating whether the given access-token is valid</returns>
        bool VerifyRefreshToken(IPrincipal principal, string applicationKey, string refreshToken);

        /// <summary>
        /// Creates or updates the refresh-token for the given user-application combination
        /// </summary>
        /// <param name="principal">the user for which to update the refresh-token</param>
        /// <param name="applicationKey">the application for which to set the new refresh-token</param>
        /// <param name="refreshToken">the new refresh-token</param>
        /// <param name="oldToken">the old refresh-token</param>
        void UpdateRefreshToken(IPrincipal principal, string applicationKey, string refreshToken, string oldToken);

        /// <summary>
        /// Revokes the given access-token for the given user and application
        /// </summary>
        /// <param name="principal">the user for which to revoke the given refresh-token</param>
        /// <param name="applicationKey">the application-key for which to revoke the token</param>
        /// <param name="refreshToken">the refresh-token that is currently set for that application-user combination</param>
        void RevokeRefreshToken(IPrincipal principal, string applicationKey, string refreshToken);
    }
}
