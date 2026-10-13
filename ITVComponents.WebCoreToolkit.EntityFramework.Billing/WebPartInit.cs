using System;
using System.Collections.Generic;
using System.Reflection;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing
{
    // Opt-in WebPart of the provider-neutral billing library. Contributes the billing catalog to the downloadable
    // system configuration and registers the agent route for card terminals. Deliberately kept here (not in the
    // Stripe part) because neither depends on a provider. Both are flag-gated and independent of each other.
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
            => config.GetSection<BillingConfigExportPartOptions>(path) ?? new BillingConfigExportPartOptions();

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [WebPartConfig] BillingConfigExportPartOptions? options)
        {
            if (options is { ActivateBillingConfigExport: true })
            {
                services.AddBillingConfigExtension();
            }

            if (options is { ActivateAgentTerminals: true })
            {
                RegisterAgentTerminals(services, options.TerminalsContextType);
            }
        }

        private static void RegisterAgentTerminals(IServiceCollection services, string? contextTypeName)
        {
            if (contextTypeName is not { Length: > 0 })
            {
                LogEnvironment.LogEvent(
                    "ActivateAgentTerminals is set, but TerminalsContextType is empty. The agent terminal route is NOT registered.",
                    LogSeverity.Error);
                return;
            }

            var contextType = (Type)ExpressionParser.Parse(contextTypeName, new Dictionary<string, object>());
            if (!typeof(IPaymentsContext).IsAssignableFrom(contextType))
            {
                LogEnvironment.LogEvent(
                    $"ActivateAgentTerminals is set, but context '{contextType.FullName}' does not implement IPaymentsContext. The agent terminal route is NOT registered.",
                    LogSeverity.Error);
                return;
            }

            typeof(TerminalDependencyInjectionExtensions)
                .GetMethod(nameof(TerminalDependencyInjectionExtensions.AddAgentTerminals), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(contextType)
                .Invoke(null, new object[] { services });
        }
    }
}
