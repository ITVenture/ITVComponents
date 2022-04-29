using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static WebPartInitOptions LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<WebPartInitOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, WebPartInitOptions options)
        {
            if (options.UseSimpleUserNameMapping)
            {
                services.UseSimpleUserNameMapping();
            }

            if (options.InitializePluginSystem)
            {
                services.InitializePluginSystem(options.UseInitPlugins);
            }

            if (options.EnablePermissionBaseAuthorization || options.EnableFeatureBasedAuthorization)
            {
                services.EnableRoleBaseAuthorization(o =>
                {
                    o.CheckFeatures = options.EnableFeatureBasedAuthorization;
                    o.CheckPermissions = options.EnablePermissionBaseAuthorization;
                });
            }

            if (options.UseContextUserAccessor)
            {
                services.UseContextUserAccessor();
            }

            if (options.UseUser2GroupMapper)
            {
                services.UseUser2GroupMapper();
            }

            if (options.UseCollectedClaimsTransformation)
            {
                services.UseCollectedClaimsTransformation();
            }

            if (options.UseRepositoryClaimsTransformation)
            {
                services.UseRepositoryClaimsTransformation(options.UseCollectedClaimsTransformation);
            }

            if (options.UseNavigator)
            {
                services.UseNavigator();
            }

            if (options.UseScopedSettings)
            {
                services.UseScopedSettings();
            }

            if (options.UseGlobalSettings)
            {
                services.UseGlobalSettings();
            }

            if (options.UseHierarchySettings)
            {
                services.UseHierarchySettings();
            }

            if (options.UseToolkitMvcMessages)
            {
                services.UseToolkitMvcMessages();
            }

            if (options.UseUrlFormatter)
            {
                services.UseUrlFormatter();
            }

            if (options.UseBackgroundTasks)
            {
                services.UseBackgroundTasks(options.TaskQueueCapacity);
            }

            if (options.UseLocalization && (options.CultureMapping.Count != 0 || options.UiCultureMapping.Count != 0))
            {
                services.ConfigureLocalization(o =>
                {
                    foreach (var p in options.UiCultureMapping)
                    {
                        o.MapUiCulture(p.IncomingCulture, p.RedirectCulture);
                    }

                    foreach (var p in options.CultureMapping)
                    {
                        o.MapCulture(p.IncomingCulture, p.RedirectCulture);
                    }
                });
            }
        }
    }
}
