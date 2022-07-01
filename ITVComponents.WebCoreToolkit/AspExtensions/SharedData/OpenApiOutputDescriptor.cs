using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public class OpenApiOutputDescriptor:IOpenApiOutputDescriptor
    {
        public OpenApiOutputDescriptor(bool isError, int code = 200, string contentType = null, string description = null,
            Type type = null)
        {
            IsError = isError;
            Code = code;
            ContentType = contentType;
            Description = description;
            Type = type;
        }

        public bool IsError { get; }
        public int Code { get; set; }
        public string ContentType { get; set; }
        public string Description { get; set; }
        public Type Type { get; set; }
    }
}
