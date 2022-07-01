using System;
using System.Collections.Generic;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Scripting.CScript.Core;
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
                if (options.PlugInDependencies != null && options.PlugInDependencies.Count != 0)
                {
                    services.ConfigurePluginFactory(o =>
                    {
                        foreach (var opt in options.PlugInDependencies)
                        {
                            var dic = new Dictionary<string, object>();
                            var tp = (Type)ExpressionParser.Parse(opt.TypeExpression, dic);
                            if (tp != null && !string.IsNullOrEmpty(opt.FriendlyName))
                            {
                                o.AddDependency(opt.FriendlyName, p => p.GetService(tp));
                            }
                        }
                    });
                }
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
                if (options.GroupClaims.Count == 0)
                {
                    services.UseUser2GroupMapper();
                }
                else
                {
                    services.UseUser2GroupMapper(o =>
                    {
                        options.GroupClaims.ForEach(n => o.SetGroupClaim(n.Key, n.Value));
                    });
                }
            }

            if (options.UseCollectedClaimsTransformation)
            {
                services.UseCollectedClaimsTransformation();
            }

            if (options.UseRepositoryClaimsTransformation)
            {
                services.UseRepositoryClaimsTransformation(options.UseCollectedClaimsTransformation);
            }

            if (options.UseSharedAssets)
            {
                services.UseAssetDrivenClaimsTransformation(options.UseCollectedClaimsTransformation);
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

            if (options.UseLocalization)
            {
                services.ConfigureLocalization(o =>
                {
                    if (options.UiCultureMapping.Count != 0)
                    {
                        foreach (var p in options.UiCultureMapping)
                        {
                            o.MapUiCulture(p.IncomingCulture, p.RedirectCulture);
                        }
                    }

                    if (options.CultureMapping.Count != 0)
                    {
                        foreach (var p in options.CultureMapping)
                        {
                            o.MapCulture(p.IncomingCulture, p.RedirectCulture);
                        }
                    }
                });
            }
        }
    }
}
