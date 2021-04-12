using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using ITVComponents.WebCoreToolkit.DbLessConfig.Configurations;
using ITVComponents.WebCoreToolkit.DbLessConfig.Security;
using ITVComponents.WebCoreToolkit.DbLessConfig.WebPlugins;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ClaimsTransformation;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection UseConfigIdentities(this IServiceCollection services, IConfiguration configuration)
        {
            return services.Configure<IdentitySettings>(configuration.GetSection(IdentitySettings.SettingsKey))
                .AddScoped<ISecurityRepository, SettingsSecurityRepository>();
        }

        public static IServiceCollection UseConfigPlugins(this IServiceCollection services, IConfiguration configuration)
        {
            return services.Configure<PluginsSettings>(configuration.GetSection(PluginsSettings.SettingsKey))
                .AddScoped<IWebPluginsSelector, PluginsSelector>();
        }
    }
}
