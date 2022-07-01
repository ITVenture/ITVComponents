using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData.Extensions
{
    public static class ParameterExtensions
    {
        public static IList<IOpenApiParameterDescriptor> Parameter(this IList<IOpenApiParameterDescriptor> list,
            string name, string description = null)
        {
            list.Add(new OpenApiParameterDescriptor(name, description));
            return list;
        }
    }
}
