using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public interface IOpenApiParameterDescriptor
    {
        public string Name { get; }

        public string Description { get; }
    }
}
