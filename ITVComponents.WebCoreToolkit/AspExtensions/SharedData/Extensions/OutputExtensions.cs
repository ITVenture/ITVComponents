using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData.Extensions
{
    public static class OutputExtensions
    {
        public static IList<IOpenApiOutputDescriptor> Output(this IList<IOpenApiOutputDescriptor> list, int code = 200,
            string contentType = null, string description = null, Type type = null)
        {
            list.Add(new OpenApiOutputDescriptor(false, code, contentType, description, type));
            return list;
        }

        public static IList<IOpenApiOutputDescriptor> Output<T>(this IList<IOpenApiOutputDescriptor> list,
            int code = 200,
            string contentType = null, string description = null)
        {
            return list.Output(code, contentType, description, typeof(T));
        }

        public static IList<IOpenApiOutputDescriptor> ErrorOutput(this IList<IOpenApiOutputDescriptor> list, int code = 200,
            string contentType = null, string description = null, Type type = null)
        {
            list.Add(new OpenApiOutputDescriptor(true, code, contentType, description, type));
            return list;
        }

        public static IList<IOpenApiOutputDescriptor> ErrorOutput<T>(this IList<IOpenApiOutputDescriptor> list,
            int code = 200,
            string contentType = null, string description = null)
        {
            return list.ErrorOutput(code, contentType, description, typeof(T));
        }
    }
}
