using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ITVComponents.WebCoreToolkit.Net.OpenApi
{
    internal class ToolkitSwaggerGenerator:IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            Console.WriteLine("Horst");
        }

        public EndPointTrunk RelevantEndPoints { get; set; }
    }
}
