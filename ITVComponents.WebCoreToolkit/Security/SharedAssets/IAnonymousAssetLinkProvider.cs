using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public interface IAnonymousAssetLinkProvider
    {
        string CreateAnonymousLink(string baseUrl, FullAssetInfo info);
    }
}
