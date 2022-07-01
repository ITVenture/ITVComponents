using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Newtonsoft;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ITVComponents.WebCoreToolkit.Net.OpenApi
{
    [WebPart]
    public static class WebPartInit
    {
        [ServiceRegistrationMethod]
        public static void RegisterServices(IServiceCollection services, [SharedObjectHeap] ISharedObjHeap sharedObjects)
        {
            /*IApiDescriptionGroupCollectionProvider
            var dokGenerator = new ToolkitSwaggerGenerator();
            services.ConfigureSwaggerGen( s => s.OperationFilterDescriptors);
            services.Configure<SwaggerGeneratorOptions>(g => g.OperationFilters.Add(dokGenerator));
            sharedObjects.Property<ToolkitSwaggerGenerator>("dokGenerator").Value = dokGenerator;*/
        }

        [EndpointMetaExpose]
        public static void RegisterEndpointsToSwagger(WebApplication builder, EndPointTrunk registeredEndPoints, [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            //sharedObjects.Property<ToolkitSwaggerGenerator>("dokGenerator").Value.RelevantEndPoints = registeredEndPoints;
            /*registeredEndPoints.ProcessEndPoints(n =>
            {
                n.RouteHandler.WithGroupName(n.DisplayName).WithDisplayName(n.DisplayName);
                foreach (var p in n.Parameters)
                {
                    n.RouteHandler.
                }
            });*/
        }

    }
}
