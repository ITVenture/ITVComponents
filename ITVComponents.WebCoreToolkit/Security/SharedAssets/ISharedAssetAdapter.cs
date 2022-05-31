using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public interface ISharedAssetAdapter
    {
        AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false);
        bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor);
        AssetTemplateInfo[] GetEligibleShares(string requestPath);
        AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title);
        bool UpdateSharedAsset(FullAssetInfo updateInfo);
        bool DeleteSharedAsset(FullAssetInfo assetInfo);
        string CreateAnonymousLink(AssetInfo info, HttpContext context);
        string CreateLink(AssetInfo info, HttpContext context);
        FullAssetInfo FindAnonymousAsset(string assetKey);
    }
}
