using System;
using System.Collections.Generic;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.BillingViews.Blazor.Options;
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

            if (key == PaymentViewsOption)
            {
                return config.GetSection<PaymentViewsPartOptions>(path) ?? new PaymentViewsPartOptions();
            }

            return null;
        }

        /// <summary>Configuration key carrying the payment-views switch.</summary>
        public const string PaymentViewsOption = "PaymentViews";

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services,
            [WebPartConfig("ContextSettings")] SecurityContextOptions? options,
            [WebPartConfig(PaymentViewsOption)] PaymentViewsPartOptions? paymentViews,
            [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
        {
            if (options is not { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
            {
                return;
            }

            var dic = new Dictionary<string, object>();
            var contextType = (Type)ExpressionParser.Parse(contextTypeName, dic);

            // ONE registration method for both view sets: several methods carrying the same aspect do not
            // reliably all run. The two are still independently switchable.
            if (options.ConfigureContext)
            {
                var method = typeof(BillingViewsExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(BillingViewsExt.AddMudBlazorBillingViews));
                method(services, partTypeLoadBehavior);
            }

            if (paymentViews is { ActivatePaymentViews: true })
            {
                var method = typeof(BillingViewsExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(BillingViewsExt.AddMudBlazorPaymentViews));
                method(services, partTypeLoadBehavior);
            }
        }
    }
}
