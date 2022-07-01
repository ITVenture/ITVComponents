using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public class OpenApiDescriptor:IOpenApiDescriptor
    {
        public OpenApiDescriptor(IEndpointConventionBuilder builder,
            string displayName = null,
            string description = null, 
            string uniqueOperationId = null,
            Action<IList<IOpenApiParameterDescriptor>> arguments = null,
            Action<IList<IOpenApiOutputDescriptor>> produces = null)
        {
            DisplayName = displayName;
            Description = description;
            UniqueOperationId = uniqueOperationId;
            Invalid = builder is not RouteHandlerBuilder;
            RouteHandler = builder as RouteHandlerBuilder;
            var tmpArg = new List<IOpenApiParameterDescriptor>();
            arguments?.Invoke(tmpArg);
            Parameters = tmpArg.ToArray();
            var tmpProd = new List<IOpenApiOutputDescriptor>();
            produces?.Invoke(tmpProd);
            Output = tmpProd.ToArray();
        }
        public RouteHandlerBuilder RouteHandler { get; }
        public bool Invalid { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string UniqueOperationId { get; }
        public IOpenApiParameterDescriptor[] Parameters { get; }
        public IOpenApiOutputDescriptor[] Output { get; }
    }
}
