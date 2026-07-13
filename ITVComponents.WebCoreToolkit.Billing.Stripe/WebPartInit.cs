using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Extensions;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Billing.Stripe
{
    // Activates the Stripe billing service layer (checkout / portal / webhook / plan-sync) purely from
    // configuration: bind the part options, resolve the host billing context type, call the generic
    // AddStripeBilling over it and map the webhook endpoint. The feature provisioner (tenant-security
    // adapter) is wired by the host separately — the webhook resolves IFeatureProvisioner from DI.
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
        {
            var options = config.GetSection<StripeBillingPartOptions>(path) ?? new StripeBillingPartOptions();
            // The Stripe secrets live under a configurable, per-host path (not hard-coded) — resolve them now
            // while we still have IConfiguration; the service-registration method only sees the loaded options.
            options.Stripe ??= config.GetSection<StripeOptions>(options.StripeConfigPath) ?? new StripeOptions();
            return options;
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig] StripeBillingPartOptions? options)
        {
            if (options is not { ActivateStripeBilling: true, ContextType: { Length: > 0 } contextTypeName })
            {
                return;
            }

            var contextType = (Type)ExpressionParser.Parse(contextTypeName, new Dictionary<string, object>());
            // AddStripeBilling<TContext> has a single generic parameter — close it directly rather than via the
            // TenantSecurity MethodHelper, so this lib stays decoupled from the tenant-security model.
            var method = typeof(DependencyInjectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(DependencyInjectionExtensions.AddStripeBilling)
                             && m.IsGenericMethodDefinition
                             && IsStripeOptionsOverload(m))
                .MakeGenericMethod(contextType);
            method.Invoke(null, new object[] { services, options.Stripe });
        }

        [EndpointRegistrationMethod]
        public static void RegisterEndpoints(WebApplication builder, [WebPartConfig] StripeBillingPartOptions? options)
        {
            if (options is { ActivateStripeBilling: true })
            {
                builder.MapStripeBillingEndpoints(options.WebhookPath);
            }
        }

        // Picks the AddStripeBilling<TContext>(IServiceCollection, StripeOptions) overload (the pre-resolved-
        // options one) over the IConfiguration-based one.
        private static bool IsStripeOptionsOverload(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return parameters.Length == 2 && parameters[1].ParameterType == typeof(StripeOptions);
        }
    }
}
