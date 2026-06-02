using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BillingViewsExt = ITVComponents.WebCoreToolkit.BillingViews.Blazor.Extensions.DependencyInjectionExtensions;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor
{
    // Registers the billing-views routing assembly + IBillingHandler for the host's context. The provider
    // service layer (AddStripeBilling) and the feature provisioner (AddBillingFeatureProvisioner) are wired
    // by the host — the handler resolves them from DI.
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object? LoadOptions(IConfiguration config, string key, string path)
        {
            if (key == "ContextSettings")
            {
                return config.GetSection<SecurityContextOptions>(path);
            }

            return null;
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ContextSettings")] SecurityContextOptions? options,
            [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
        {
            if (options is { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
            {
                var dic = new Dictionary<string, object>();
                var contextType = (Type)ExpressionParser.Parse(contextTypeName, dic);
                var method = typeof(BillingViewsExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(BillingViewsExt.AddMudBlazorBillingViews));
                method(services, partTypeLoadBehavior);
            }
        }
    }
}
