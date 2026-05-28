using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Authentication.ApiKey.Models;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.WebCoreToolkit.Authentication.ApiKey
{
    public class DefaultApiKeyUserResolver:IGetApiKeyQuery
    {
        private readonly ISecurityRepository securityRepository;

        public DefaultApiKeyUserResolver(ISecurityRepository securityRepository)
        {
            this.securityRepository = securityRepository;
        }

        public Task<Models.ApiKey> Execute(string providedApiKey, string authenticationScheme)
        {
            var apiKeyUser = securityRepository.Users.FirstOrDefault(n => n.UserName.Equals(providedApiKey, StringComparison.OrdinalIgnoreCase) && n.AuthenticationType == authenticationScheme);
            if (apiKeyUser != null)
            {
                Models.ApiKey retVal = new Models.ApiKey(providedApiKey, DateTime.Now);
                return Task.FromResult(retVal);
            }

            return Task.FromResult<Models.ApiKey>(null);
        }
    }
}
