using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public interface IOpenApiDescriptor
    {
        RouteHandlerBuilder RouteHandler { get; }

        bool Invalid { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public IOpenApiParameterDescriptor[] Parameters { get; }

        public IOpenApiOutputDescriptor[] Output { get; }
        string UniqueOperationId { get; }
    }
}
