using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public class OpenApiParameterDescriptor: IOpenApiParameterDescriptor
    {
        private readonly string name;
        private readonly string description;

        public OpenApiParameterDescriptor(string name, string description)
        {
            this.name = name;
            this.description = description;
        }

        public string Name { get; }
        public string Description { get; }
    }
}
