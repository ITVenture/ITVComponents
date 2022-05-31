using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Extensions;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string optionType, string path)
        {
            switch (optionType)
            {
                case "OpenId":
                    return config.GetSection<OpenIdConnectOptions>(path);
                case "Microsoft":
                    return config.GetSection<MicrosoftConnectOptions>(path);
                case "Google":
                    return config.GetSection<GoogleConnectOptions>(path);
                case "Facebook":
                    return config.GetSection<FacebookConnectOptions>(path);
                case "Bearer":
                    return config.GetSection<BearerConnectOptions>(path);
            }

            return null;
        }

        [AuthenticationRegistrationMethod]
        public static void RegisterAuthenticator(AuthenticationBuilder auth, 
            [WebPartConfig("OpenId")] OpenIdConnectOptions openIdConfig,
            [WebPartConfig("Microsoft")] MicrosoftConnectOptions microsoftConfig,
            [WebPartConfig("Google")] GoogleConnectOptions googleConfig,
            [WebPartConfig("Facebook")] FacebookConnectOptions facebookConfig,
            [WebPartConfig("Bearer")] BearerConnectOptions bearerConfig)
        {
            if (openIdConfig != null)
            {
                auth.OpenIdQuick(openIdConfig);
            }

            if (microsoftConfig != null)
            {
                auth.MicrosoftQuick(microsoftConfig);
            }
        }
    }
}
