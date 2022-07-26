using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Formatters;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Handlers
{
    public static class HealthHandler<TAppInfo, TReadynessInfo, TLivenessInfo>
    {
        /// <summary>
        /// Application Info
        /// </summary>
        /// <remarks>Returns the app version currently deployed. This endpoint can be used to check the availability of the service.</remarks>
        /// <response code="200">API is healthy</response>
        /// <response code="503">API is unhealthy or in degraded state</response>
        public static async Task<IResult> AppInfo(HttpContext context)
        {
            
            var healthService = context.RequestServices.GetService<HealthCheckService>();
            var appInfoOptions = context.RequestServices.GetService<IOptions<AppInfoOptions>>();
            var appInfoFormatter = context.RequestServices.GetService<IAppInfoFormatter<TAppInfo>>();
            var report = await healthService.CheckHealthAsync();
            return Results.Json(appInfoFormatter.FormatAppInfo(appInfoOptions.Value, report),
                statusCode: (int)(report.Status == HealthStatus.Healthy
                    ? HttpStatusCode.OK
                    : HttpStatusCode.ServiceUnavailable)
                , options: !appInfoOptions.Value.UseCamelCase ? new JsonSerializerOptions() : null);
        }

        /// <summary>
        ///     Readyness check
        /// </summary>
        /// <remarks>returns "UP" when ready. To be used as readyness probe as specified by OpenShift Container Platform. A readiness probe determines if a container is ready to service requests.</remarks>              
        /// <response code="200">Service is ready</response>
        /// <response code="503">Service is unhealthy or in degraded state</response>
        public static async Task<IResult> Ready(HttpContext context)
        {
            var healthService = context.RequestServices.GetService<HealthCheckService>();
            var appInfoOptions = context.RequestServices.GetService<IOptions<AppInfoOptions>>();
            var appReadyFormatter = context.RequestServices.GetService<IAppInfoFormatter<TReadynessInfo>>();
            var report = await healthService.CheckHealthAsync();
            return Results.Json(appReadyFormatter.FormatAppInfo(appInfoOptions.Value, report),
                statusCode: (int)(report.Status == HealthStatus.Healthy
                    ? HttpStatusCode.OK
                    : HttpStatusCode.ServiceUnavailable)
                , options:!appInfoOptions.Value.UseCamelCase?new JsonSerializerOptions():null);
        }

        /// <summary>
        ///     Liveness check
        /// </summary>
        /// <remarks>returns "UP" when alive. To be used as liveness probe as specified by OpenShift Container Platform. A liveness probe checks if the container is still running.</remarks>
        /// <response code="200">API is healthy</response>
        /// <response code="503">API is unhealthy or in degraded state</response>
        public static async Task<IResult> Live(HttpContext context)
        {
            var healthService = context.RequestServices.GetService<HealthCheckService>();
            var appInfoOptions = context.RequestServices.GetService<IOptions<AppInfoOptions>>();
            var appLiveFormatter = context.RequestServices.GetService<IAppInfoFormatter<TLivenessInfo>>();
            var report = await healthService.CheckHealthAsync();
            return Results.Json(appLiveFormatter.FormatAppInfo(appInfoOptions.Value, report),
                statusCode: (int)(report.Status == HealthStatus.Healthy
                    ? HttpStatusCode.OK
                    : HttpStatusCode.ServiceUnavailable)
                , options: !appInfoOptions.Value.UseCamelCase ? new JsonSerializerOptions() : null);
        }

        public static async Task AppInfo(HttpContext context, HealthReport report)
        {
            var appInfoOptions = context.RequestServices.GetService<IOptions<AppInfoOptions>>();
            var appInfoFormatter = context.RequestServices.GetService<IAppInfoFormatter<TAppInfo>>();
            JsonHelper.WriteObject(appInfoFormatter.FormatAppInfo(appInfoOptions.Value, report), Encoding.UTF8,
                context.Response.Body, useCamelCase:appInfoOptions.Value.UseCamelCase);
        }

        public static async Task Ready(HttpContext context, HealthReport report)
        {
            var appInfoOptions = context.RequestServices.GetService<IOptions<AppInfoOptions>>();
            var appReadyFormatter = context.RequestServices.GetService<IAppInfoFormatter<TReadynessInfo>>();
            JsonHelper.WriteObject(appReadyFormatter.FormatAppInfo(appInfoOptions.Value, report), Encoding.UTF8,
                context.Response.Body, useCamelCase: appInfoOptions.Value.UseCamelCase);
        }

        public static async Task Live(HttpContext context, HealthReport report)
        {
            var appInfoOptions = context.RequestServices.GetService<IOptions<AppInfoOptions>>();
            var appLiveFormatter = context.RequestServices.GetService<IAppInfoFormatter<TLivenessInfo>>();
            JsonHelper.WriteObject(appLiveFormatter.FormatAppInfo(appInfoOptions.Value, report), Encoding.UTF8,
                context.Response.Body, useCamelCase: appInfoOptions.Value.UseCamelCase);
        }
    }
}
