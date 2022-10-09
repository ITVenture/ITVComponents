using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebPartOptions = ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Options.WebPartOptions;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<WebPartOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig] WebPartOptions webPartCfg, [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
            l.Value.AddIfMissing(webPartCfg.AuthenticationType, true);
            services.UseDefaultApiKeyResolver();
        }

        [AuthenticationRegistrationMethod]
        public static void RegisterAuthenticator(AuthenticationBuilder auth, WebPartOptions webPartCfg)
        {
            webPartCfg ??= new();
            auth.AddApiKeySupport(o => o.AuthenticationType = webPartCfg.AuthenticationType);
        }
    }
}
