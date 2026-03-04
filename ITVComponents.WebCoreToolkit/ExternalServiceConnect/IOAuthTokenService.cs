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
    }
}
