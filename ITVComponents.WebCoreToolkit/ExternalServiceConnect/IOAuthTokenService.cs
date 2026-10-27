using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect
{
    public interface IOAuthTokenService
    {
        string GetValidAccessToken(string connectionName);
        Task<string> GetValidAccessTokenAsync(string connectionName);

        /// <summary>
        /// Revokes the stored token of the current tenant: marks it revoked locally and - where the service has a
        /// revocation endpoint - revokes the refresh token at the provider.
        /// </summary>
        Task RevokeAsync(string connectionName);
    }
}
