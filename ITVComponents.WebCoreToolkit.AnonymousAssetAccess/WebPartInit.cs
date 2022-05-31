using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Extensions;
using ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Options;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebPartOptions = ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Options.WebPartOptions;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess
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
        public static void RegisterServices(IServiceCollection services, WebPartOptions webPartCfg)
        {
            services.UseDefaultAnonymousAssetResolver()
                .Configure<AnonymousLinkSettings>(s => s.MaximumLinkDuration = webPartCfg.MaxAnonymousLinkAge)
                .Configure<ToolkitPolicyOptions>(o => o.SignInSchemes.Add(webPartCfg.AuthenticationType));
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
