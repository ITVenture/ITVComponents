using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters.Impl;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Model;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection UseDefaultAppInfoFormatter(this IServiceCollection services)
        {
            return services.AddScoped<IAppInfoFormatter<AppInfoModel>, DefaultAppInfoFormatter>();
        }

        public static IServiceCollection UseDefaultAppReadyFormatter(this IServiceCollection services)
        {
            return services.AddScoped<IAppInfoFormatter<ReadynessModel>, DefaultAppReadyFormatter>();
        }

        public static IServiceCollection UseDefaultAppLiveFormatter(this IServiceCollection services)
        {
            return services.AddScoped<IAppInfoFormatter<LivenessModel>, DefaultAppLiveFormatter>();
        }
    }
}
