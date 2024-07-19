using System;
using System.Collections.Generic;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string settingsKey, string path)
        {
            if (settingsKey == "ContextSettings")
            {
                return config.GetSection<SecurityContextOptions>(path);
            }

            if (settingsKey == "ActivationSettings")
            {
                var retVal = config.GetSection<ActivationOptions>(path);
                config.RefResolve(retVal);
                if (retVal.ActivateDbContext)
                {
                    retVal.ConnectionStringName = config.GetConnectionString(retVal.ConnectionStringName);
                }

                return retVal;
            }

            return null;//config.GetSection<UserViewOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig("ContextSettings")]SecurityContextOptions contextOptions,
            [WebPartConfig("ActivationSettings")]ActivationOptions partActivation,
            [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            Type t = null;
            if (!string.IsNullOrEmpty(contextOptions.ContextType))
            {
                var dic = new Dictionary<string, object>();
                t = (Type)ExpressionParser.Parse(contextOptions.ContextType, dic);
            }

            if (partActivation.ActivateDbContext)
            {
                if (partActivation.UseApplicationIdentitySchema)
                {
                    var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
                    l.Value.AddIfMissing(IdentityConstants.ApplicationScheme, true);
                }
            }

            if (partActivation.UseNavigation)
            {
                if (t != null)
                {
                    services.UseDbNavigation(t);
                }
                else
                {
                    services.UseDbNavigation();
                }
            }

            if (partActivation.UseSharedAssets)
            {
                if (t != null)
                {
                    services.UseDbSharedAssets(t);
                }
                else
                {
                    services.UseDbSharedAssets();
                }
            }

            if (partActivation.UsePlugins)
            {
                services.UseDbPlugins(partActivation.PluginBufferDuration);
            }

            if (partActivation.UseGlobalSettings)
            {
                services.UseDbGlobalSettings();
            }

            if (partActivation.UseTenantSettings)
            {
                services.UseTenantSettings();
            }

            if (partActivation.UseLogAdapter)
            {
                services.UseDbLogAdapter();
            }

            if (partActivation.UseApplicationTokens)
            {
                if (t != null)
                {
                    services.UseApplicationTokenService(t);
                }
                else
                {
                    services.UseApplicationTokenService();
                }
            }
            /*if (!string.IsNullOrEmpty(options?.ContextType))
            {
                var dic = new Dictionary<string, object>();
                var t = (Type)ExpressionParser.Parse(options.ContextType, dic);
                manager.EnableItvUserView(t);
            }
            else
            {
                manager.EnableItvUserView();
            }*/
        }

        [HealthCheckRegistration]
        public static void RegisterHealthChecks(IHealthChecksBuilder builder,
            [WebPartConfig("ActivationSettings")] ActivationOptions partActivation)
        {
            if (partActivation.UseHealthChecks)
            {
                foreach (var item in partActivation.HealthChecks)
                {
                    bool apply = true;
                    if (!string.IsNullOrEmpty(item.UseExpression) && item.ConditionVariables != null)
                    {
                        apply = (bool)ExpressionParser.Parse(item.UseExpression, item.ConditionVariables);
                    }

                    if (apply)
                    {
                        builder.AddScriptedCheck(item.Label);
                    }
                }
            }
        }
    }
}
