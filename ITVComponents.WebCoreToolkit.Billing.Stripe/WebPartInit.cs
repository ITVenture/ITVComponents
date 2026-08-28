using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Extensions;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Options;
using ITVComponents.WebCoreToolkit.Billing.Stripe.Payments.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
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
            if (options?.Stripe == null)
            {
                return;
            }

            // ONE registration method for both axes on purpose: several methods carrying the same aspect do not
            // reliably all run. The two axes are still independent of each other — a host can activate either.
            if (options is { ActivateStripeBilling: true, ContextType: { Length: > 0 } contextTypeName })
            {
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

            if (options.ActivatePayments)
            {
                RegisterPayments(services, options);
            }
        }

        [EndpointRegistrationMethod]
        public static void RegisterEndpoints(WebApplication builder, [WebPartConfig] StripeBillingPartOptions? options)
        {
            if (options is { ActivateStripeBilling: true })
            {
                builder.MapStripeBillingEndpoints(options.WebhookPath);
            }

            if (options is { ActivatePayments: true })
            {
                builder.MapStripeConnectEndpoints(options.ConnectWebhookPath, options.ConnectReturnPath,
                    options.ConnectRefreshPath, options.PaymentsManagePath);
            }
        }

        /// <summary>
        /// Closes the payments registrations over the configured context. Falls back to the billing context when
        /// no separate one is named — the usual case, where one host context carries both axes.
        /// </summary>
        private static void RegisterPayments(IServiceCollection services, StripeBillingPartOptions options)
        {
            var typeName = options.PaymentsContextType ?? options.ContextType;
            if (typeName is not { Length: > 0 })
            {
                return;
            }

            var contextType = (Type)ExpressionParser.Parse(typeName, new Dictionary<string, object>());
            var addPayments = typeof(PaymentsDependencyInjectionExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == nameof(PaymentsDependencyInjectionExtensions.AddStripePayments)
                             && m.IsGenericMethodDefinition
                             && IsStripeOptionsOverload(m))
                .MakeGenericMethod(contextType);
            addPayments.Invoke(null, new object[] { services, options.Stripe! });

            if (!options.ActivateVolumeWaiver)
            {
                return;
            }

            if (!typeof(IBillingContext).IsAssignableFrom(contextType))
            {
                // The waiver writes onto the tenant's own subscription invoice, so it needs both axes in one
                // context. Saying so beats registering nothing and letting the fee simply never be waived.
                LogEnvironment.LogEvent(
                    $"The volume waiver is activated, but the payments context {contextType.FullName} does not implement IBillingContext. The waiver needs both axes in one context and is NOT registered.",
                    LogSeverity.Error, "StripeConnect");
                return;
            }

            typeof(PaymentsDependencyInjectionExtensions)
                .GetMethod(nameof(PaymentsDependencyInjectionExtensions.AddStripePaymentsWaiver), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(contextType)
                .Invoke(null, new object[] { services });
        }

        // Picks the (IServiceCollection, StripeOptions) overload (the pre-resolved-options one) over the
        // IConfiguration-based one.
        private static bool IsStripeOptionsOverload(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return parameters.Length == 2 && parameters[1].ParameterType == typeof(StripeOptions);
        }
    }
}
