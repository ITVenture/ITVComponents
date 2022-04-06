using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection ConfigureTenantViews(this IServiceCollection services, Action<SecurityViewsOptions> configure)
        {
            return services.Configure(configure);
        }
    }
}
