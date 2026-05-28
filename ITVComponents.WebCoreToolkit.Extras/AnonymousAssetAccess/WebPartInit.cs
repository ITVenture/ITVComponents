using System.Collections.Generic;
using Dynamitey.DynamicObjects;
using ITVComponents.Helpers;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Extensions;
using ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Options;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebPartOptions = ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess.Options.WebPartOptions;

namespace ITVComponents.WebCoreToolkit.Extras.AnonymousAssetAccess
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
        public static void RegisterServices(IServiceCollection services, [WebPartConfig]WebPartOptions webPartCfg, [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
            l.Value.AddIfMissing(webPartCfg.AuthenticationType, true);
            services.UseDefaultAnonymousAssetResolver()
                .Configure<AnonymousLinkSettings>(s => s.MaximumLinkDuration = webPartCfg.MaxAnonymousLinkAge);
        }

        [AuthenticationRegistrationMethod]
        public static void RegisterAuthenticator(AuthenticationBuilder auth, WebPartOptions webPartCfg)
        {
            webPartCfg ??= new();
            auth.AddAnonymousAssetSupport(o =>
            {
                o.AuthenticationType = webPartCfg.AuthenticationType;
            });
        }
    }
}
