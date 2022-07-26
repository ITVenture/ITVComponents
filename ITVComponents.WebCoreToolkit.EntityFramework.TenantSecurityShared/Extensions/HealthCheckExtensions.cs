using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Health;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class HealthCheckExtensions
    {
        public static IHealthChecksBuilder AddScriptedCheck(this IHealthChecksBuilder builder, string name)
        {
            return builder.AddCheck<ScriptedHealthCheck>(name);
        }
    }
}
