using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Handlers;
using Microsoft.AspNetCore.Builder;

namespace ITVComponents.WebCoreToolkit.Net.OpenShiftHealth.Extensions
{
    public static class RouteExtensions
    {
        /// <summary>
        /// Exposes an Endpoint that enables a caller to request diagnostics information 
        /// </summary>
        /// <param name="builder">the route builder used to expose the consumer-endpoint</param>
        /// <param name="basePath">the base path under which the health-point is exposed</param>
        /// <returns>the resulting ConventionBuilder for further configuration</returns>
        public static void ExposeHealthEndPoints<TAppInfo, TReadyInfo, TLiveInfo>(this WebApplication builder, string basePath, out IEndpointConventionBuilder appInfoPoint, out IEndpointConventionBuilder appReadyPoint, out IEndpointConventionBuilder appLivePoint)
        {
            string appInfoPath = $"{basePath}/app/info";
            string appReadyPath = $"{basePath}/ready";
            string appLivePath = $"{basePath}/live";
            appInfoPoint = builder.MapGet(appInfoPath, new Func<HttpContext, Task<IResult>>(HealthHandler<TAppInfo,TReadyInfo,TLiveInfo>.AppInfo))
                .Produces<Dictionary<string, object>>()
                .Produces<Dictionary<string, object>>(503);
            appReadyPoint = builder.MapGet(appReadyPath, new Func<HttpContext, Task<IResult>>(HealthHandler<TAppInfo, TReadyInfo, TLiveInfo>.Ready))
                .Produces<Dictionary<string, object>>()
                .Produces<Dictionary<string, object>>(503);
            appLivePoint = builder.MapGet(appLivePath, new Func<HttpContext, Task<IResult>>(HealthHandler<TAppInfo, TReadyInfo, TLiveInfo>.Live))
                .Produces<Dictionary<string, object>>()
                .Produces<Dictionary<string, object>>(503);
        }
    }

    /// <summary>
    /// Helper delegate used for exposing the HealthPoints through the WebPart system
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="basePath"></param>
    /// <param name="appInfoPoint"></param>
    /// <param name="appReadyPoint"></param>
    /// <param name="appLivePoint"></param>
    public delegate void ExposeHealthEndPointsCallback(WebApplication builder, string basePath,
        out IEndpointConventionBuilder appInfoPoint, out IEndpointConventionBuilder appReadyPoint,
        out IEndpointConventionBuilder appLivePoint);
}
