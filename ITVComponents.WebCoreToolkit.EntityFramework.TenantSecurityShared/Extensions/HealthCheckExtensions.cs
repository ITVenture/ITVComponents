using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class HealthCheckExtensions
    {
        public static IHealthChecksBuilder AddScriptedCheck<TTrustConfig>(this IHealthChecksBuilder builder, string name) where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
        {
            return builder.AddScriptedCheck<ScriptedHealthCheck<TTrustConfig>, TTrustConfig>(name);
        }

        public static IHealthChecksBuilder AddScriptedCheck<THealthCheck, TTrustConfig>(this IHealthChecksBuilder builder, string name)
            where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
            where THealthCheck : ScriptedHealthCheck<TTrustConfig>
        {
            return builder.AddCheck<THealthCheck>(name);
        }
    }
}
