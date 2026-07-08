using System;
using System.Reflection;
using ITVComponents.Json;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.EFRepo.DataSync
{
    public static class ConfigExtensionServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a system-config export extension. Reads the <see cref="SystemConfigHandlerAttribute"/> on
        /// <typeparamref name="TMarkup"/>, wires native-polymorphism for the markup type (so the section round-trips
        /// as typed JSON) and records the handler in <see cref="ConfigExtensionOptions"/>. The
        /// <c>ExtendNativeProtocolType</c> call is a one-time, process-global registration — intended for startup.
        /// </summary>
        public static IServiceCollection AddSystemConfigExtension<TMarkup>(this IServiceCollection services)
            where TMarkup : ConfigExtensionMarkup
        {
            var attr = typeof(TMarkup).GetCustomAttribute<SystemConfigHandlerAttribute>()
                       ?? throw new InvalidOperationException($"{typeof(TMarkup).Name} must be annotated with [SystemConfigHandler].");
            if (!typeof(IConfigExtension).IsAssignableFrom(attr.HandlerType))
            {
                throw new InvalidOperationException($"{attr.HandlerType.Name} must implement {nameof(IConfigExtension)}.");
            }

            JsonHelper.ExtendNativeProtocolType<ConfigExtensionMarkup, TMarkup>(attr.SectionKey);
            services.Configure<ConfigExtensionOptions>(o =>
                o.Handlers.Add(new ConfigExtensionRegistration(attr.SectionKey, typeof(TMarkup), attr.HandlerType)));
            return services;
        }
    }
}
