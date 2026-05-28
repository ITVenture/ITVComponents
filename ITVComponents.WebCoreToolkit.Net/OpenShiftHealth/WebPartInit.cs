using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.SettingsExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Handlers;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Extensions;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static ActivationSettings LoadConfiguration(IConfiguration config, string path)
        {
            var retVal =config.GetSection<ActivationSettings>(path);
            config.RefResolve(retVal);
            return retVal;
        }

        [EndpointRegistrationMethod]
        public static void RegisterHealthEndPoints(WebApplication builder, ActivationSettings options)
        {
            if (options.ExposeHealthEndPoints && options.UseBuiltInMiddleware)
            {
                ResponseFormatterBuilder.BuildCallbacks(options, out var appInfo, out var readyInfo, out var liveInfo);
                builder.MapHealthChecks(new PathString($"{options.HealthBasePath}/app/info"), new HealthCheckOptions
                {
                    AllowCachingResponses = false,
                    ResultStatusCodes = new Dictionary<HealthStatus, int>()
                    {
                        { HealthStatus.Healthy, 200 },
                        { HealthStatus.Degraded, 503 },
                        { HealthStatus.Unhealthy, 503 }
                    },
                    ResponseWriter = appInfo
                }).WithGroupName(options.HealthBasePath).WithDisplayName("App-Info").WithName("AppInfo");

                builder.MapHealthChecks(new PathString($"{options.HealthBasePath}/ready"), new HealthCheckOptions
                {
                    AllowCachingResponses = false,
                    ResultStatusCodes = new Dictionary<HealthStatus, int>()
                    {
                        { HealthStatus.Healthy, 200 },
                        { HealthStatus.Degraded, 503 },
                        { HealthStatus.Unhealthy, 503 }
                    },
                    ResponseWriter = readyInfo
                }).WithGroupName(options.HealthBasePath).WithDisplayName("Readyness").WithName("Ready");

                builder.MapHealthChecks(new PathString($"{options.HealthBasePath}/live"), new HealthCheckOptions
                {
                    AllowCachingResponses = false,
                    ResultStatusCodes = new Dictionary<HealthStatus, int>()
                    {
                        { HealthStatus.Healthy, 200 },
                        { HealthStatus.Degraded, 503 },
                        { HealthStatus.Unhealthy, 503 }
                    },
                    ResponseWriter = liveInfo
                }).WithGroupName(options.HealthBasePath).WithDisplayName("Liveness").WithName("Live");
            }
            else if (options.ExposeHealthEndPoints)
            {
                var exposeEndPoints = ResponseFormatterBuilder.BuildHealthExposer(options);
                exposeEndPoints(builder, options.HealthBasePath, out _, out _, out _);
            }
        }

        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, ActivationSettings options)
        {
            if (options.UseDefaultAppInfoFormatter)
            {
                services.UseDefaultAppInfoFormatter();
            }

            if (options.UseDefaultAppLiveFormatter)
            {
                services.UseDefaultAppLiveFormatter();
            }

            if (options.UseDefaultAppReadyFormatter)
            {
                services.UseDefaultAppReadyFormatter();
            }

            services.Configure<AppInfoOptions>(o =>
            {
                o.Name = options.Name;
                o.Description = options.Description;
                o.Version = options.Version;
                o.UseCamelCase = options.UseCamelCase;
            });
        }
    }
}
