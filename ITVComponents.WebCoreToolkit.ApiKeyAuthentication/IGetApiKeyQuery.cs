using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Models;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication
{
    public interface IGetApiKeyQuery
    {
        Task<ApiKey> Execute(string providedApiKey, string authenticationScheme);
    }
}
