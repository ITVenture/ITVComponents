using System;
using ITVComponents.EFRepo.Helpers;
using ITVComponents.Logging;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    /// <summary>
    /// Shared handling of the <see cref="ActivationOptions"/> for all WebParts that read them (TenantSecurityShared, TenantTreeShared, ...),
    /// so that reference-resolution and the custom schema behave identically regardless of which WebPart a host loads.
    /// </summary>
    public static class ActivationOptionsHelper
    {
        /// <summary>
        /// Reads the ActivationOptions from the given configuration-path and resolves all references, including the CustomSchema
        /// </summary>
        /// <param name="config">the application-configuration</param>
        /// <param name="path">the path of the ActivationSettings</param>
        /// <returns>the loaded and resolved ActivationOptions</returns>
        public static ActivationOptions ResolveActivationOptions(this IConfiguration config, string path)
        {
            var retVal = config.GetSection<ActivationOptions>(path);
            var rawSchema = retVal.CustomSchema;
            config.RefResolve(retVal);
            // RefResolve handles :--> itself, but silently leaves an unresolvable reference empty. The ->-notation
            // (as used by the plugin-side CustomSchemaOptionsLoader) is unknown to RefResolve and goes through the shared helper.
            if (!string.IsNullOrEmpty(rawSchema) && rawSchema.StartsWith("->"))
            {
                retVal.CustomSchema = SchemaConfiguration.Resolve(rawSchema, config);
            }
            else if (!string.IsNullOrEmpty(rawSchema) && rawSchema.StartsWith(":-->") && string.IsNullOrEmpty(retVal.CustomSchema))
            {
                LogEnvironment.LogEvent($"The schema-reference '{rawSchema}' in the ActivationSettings did not resolve to a value. The default schema of the model is used.", LogSeverity.Warning);
            }

            return retVal;
        }

        /// <summary>
        /// Applies the CustomSchema of the given ActivationOptions to the security-context
        /// </summary>
        /// <param name="services">the services where the schema-redirect is configured</param>
        /// <param name="contextType">the type of the security-context. When null, a configured schema can not be applied</param>
        /// <param name="options">the resolved ActivationOptions</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection ApplyCustomSchema(this IServiceCollection services, Type contextType, ActivationOptions options)
        {
            if (string.IsNullOrEmpty(options.CustomSchema))
            {
                return services;
            }

            if (contextType == null)
            {
                LogEnvironment.LogEvent($"A CustomSchema ('{options.CustomSchema}') is configured in the ActivationSettings, but no context-type is configured (ContextSettings.ConfigureContext/ContextType). The schema is not applied.", LogSeverity.Warning);
                return services;
            }

            return services.ConfigureCustomTenantSecuritySchema(contextType, options.CustomSchema);
        }
    }
}
