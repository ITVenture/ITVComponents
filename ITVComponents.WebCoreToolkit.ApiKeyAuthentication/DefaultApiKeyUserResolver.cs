using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication
{
    public class DefaultApiKeyUserResolver:IGetApiKeyQuery
    {
        private readonly ISecurityRepository securityRepository;

        public DefaultApiKeyUserResolver(ISecurityRepository securityRepository)
        {
            this.securityRepository = securityRepository;
        }

        public Task<ApiKey> Execute(string providedApiKey, string authenticationScheme)
        {
            var apiKeyUser = securityRepository.Users.FirstOrDefault(n => n.UserName.Equals(providedApiKey, StringComparison.OrdinalIgnoreCase) && n.AuthenticationType == authenticationScheme);
            if (apiKeyUser != null)
            {
                ApiKey retVal = new ApiKey(providedApiKey, DateTime.Now);
                return Task.FromResult(retVal);
            }

            return Task.FromResult<ApiKey>(null);
        }
    }
}
