using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Authentication.ApiKey.Models;

namespace ITVComponents.WebCoreToolkit.Authentication.ApiKey
{
    public interface IGetApiKeyQuery
    {
        Task<Models.ApiKey> Execute(string providedApiKey, string authenticationScheme);
    }
}
