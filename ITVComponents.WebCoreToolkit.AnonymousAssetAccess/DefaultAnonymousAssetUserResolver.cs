using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Models;
using ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Options;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess
{
    public class DefaultAnonymousAssetUserResolver : IGetAnonymousAssetQuery, IAnonymousAssetLinkProvider
    {
        public const string SecurityTokenParamName = "__AccessToken";
        private readonly ISecurityRepository securityRepository;
        private readonly ISharedAssetAdapter assetAdapter;
        private readonly IOptions<AnonymousLinkSettings> options;
        private readonly ILogger<DefaultAnonymousAssetUserResolver> logger;

        public DefaultAnonymousAssetUserResolver(ISecurityRepository securityRepository, ISharedAssetAdapter assetAdapter, IOptions<AnonymousLinkSettings> options, ILogger<DefaultAnonymousAssetUserResolver> logger)
        {
            this.securityRepository = securityRepository;
            this.assetAdapter = assetAdapter;
            this.options = options;
            this.logger = logger;
        }

        public AnonymousAsset Execute(IQueryCollection providedQuery, out bool denied)
        {
            if (providedQuery.ContainsKey(Global.FixedAssetRequestQueryParameter) &&
                providedQuery.ContainsKey(SecurityTokenParamName))
            {
                string assetKey = providedQuery[Global.FixedAssetRequestQueryParameter];
                string securitytoken = providedQuery[SecurityTokenParamName];
                var asset = assetAdapter.FindAnonymousAsset(assetKey);
                denied = !ValidateAnonymousToken(securitytoken, asset);
                if (!denied)
                {
                    AnonymousAsset retVal = new AnonymousAsset("#ANONYMOUS#", DateTime.Now);
                    return retVal;
                }

                return null;
            }

            denied = false;
            return null;
        }

        public string CreateAnonymousLink(string baseUrl, FullAssetInfo info)
        {
            var quid = baseUrl.Contains('?');
            var nop = !baseUrl.EndsWith('?');
            var raw = securityRepository.Encrypt(
                Encoding.UTF8.GetBytes(
                    $"{info.AnonymousAccessTokenRaw}#{DateTime.Now:yyyyMMddHHmmssfff}#{info.NotBefore:yyyyMMdd}#{info.NotAfter:yyyyMMdd}"),
                info.UserScopeName);
            var ret = WebEncoders.Base64UrlEncode(raw);
            return
                $"{baseUrl}{(!nop ? (quid ? "&" : "?") : string.Empty)}{SecurityTokenParamName}={ret}";
        }

        private bool ValidateAnonymousToken(string token, FullAssetInfo info)
        {
            var rawBase = WebEncoders.Base64UrlDecode(token);
            byte[] raw = null;
            try{raw=securityRepository.Decrypt(rawBase, info.UserScopeName);}catch(Exception ex){logger.LogError(ex, "Failed to decrypt Token");}

            if (raw != null)
            {
                var rawString = Encoding.UTF8.GetString(raw);
                var segments = rawString.Split("#");
                if (segments.Length == 4)
                {
                    var retVal = segments[0] == info.AnonymousAccessTokenRaw;
                    DateTime? notBefore = null;
                    DateTime? notAfter = null;
                    DateTime linkCreated = DateTime.ParseExact(segments[1], "yyyyMMddHHmmssfff", null);
                    if (!string.IsNullOrEmpty(segments[2]))
                    {
                        notBefore = DateTime.ParseExact(segments[2], "yyyyMMdd", null);
                    }

                    if (!string.IsNullOrEmpty(segments[3]))
                    {
                        notAfter = DateTime.ParseExact(segments[3], "yyyyMMdd", null);
                    }

                    retVal &= info.NotBefore == notBefore;
                    retVal &= info.NotAfter == notAfter;
                    retVal &= options.Value.MaximumLinkDuration > DateTime.Now.Subtract(linkCreated).TotalDays;
                    return retVal;
                }
            }

            return false;
        }
    }
}
