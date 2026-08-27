using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Models;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Options;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess
{
    public class DefaultAnonymousAssetUserResolver : IGetAnonymousAssetQuery, IAnonymousAssetLinkProvider
    {
        /// <summary>
        /// Der Name des Query-Parameters der abgekuendigten Form. Bleibt als Alias auf die zentrale
        /// Konstante stehen, weil Hosts ihn benutzen.
        /// </summary>
        public const string SecurityTokenParamName = Global.FixedAssetTokenQueryParameter;

        /// <summary>
        /// Der Name, unter dem ein anonymer Asset-Besucher auftritt.
        /// <para>
        /// <b>Nicht zu verwechseln mit dem Filter-Platzhalter</b> <c>##ANONYMOUS</c>
        /// (<c>SharedAssetInfoProvider.AnonymousTag</c>): der steht AM ASSET und sagt, dass es anonym
        /// geteilt werden darf. Dieser hier ist bloss der Name, unter dem der Besucher dann auftritt -
        /// zwei verschiedene Zeichenketten mit zwei verschiedenen Aufgaben, und sie werden nirgends
        /// miteinander verglichen.
        /// </para>
        /// </summary>
        public const string AnonymousUserLabel = "#ANONYMOUS#";
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
        
        public AnonymousAsset Execute(string assetKey, string accessToken, out bool denied)
        {
            if (!string.IsNullOrEmpty(assetKey) && !string.IsNullOrEmpty(accessToken))
            {
                var asset = assetAdapter.FindAnonymousAsset(assetKey);
                if (asset == null)
                {
                    // Ein Token ohne passendes Asset ist kein "kein anonymer Zugriff", sondern ein
                    // abgelehnter - sonst faellt der Aufrufer stillschweigend auf den anonymen Benutzer
                    // zurueck und der Fehler taucht erst als fehlende Berechtigung wieder auf.
                    logger.LogWarning("No anonymously shared asset found for the provided key.");
                    denied = true;
                    return null;
                }

                denied = !ValidateAnonymousToken(accessToken, asset);
                if (!denied)
                {
                    AnonymousAsset retVal = new AnonymousAsset(AnonymousUserLabel, DateTime.Now);
                    return retVal;
                }

                logger.LogWarning("The provided access-token did not validate for the requested asset.");
                return null;
            }

            denied = false;
            return null;
        }

        public string CreateAnonymousToken(FullAssetInfo info)
        {
            var raw = securityRepository.Encrypt(
                Encoding.UTF8.GetBytes(
                    $"{info.AnonymousAccessTokenRaw}#{DateTime.Now:yyyyMMddHHmmssfff}#{info.NotBefore:yyyyMMdd}#{info.NotAfter:yyyyMMdd}"),
                info.UserScopeName);
            return WebEncoders.Base64UrlEncode(raw);
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
