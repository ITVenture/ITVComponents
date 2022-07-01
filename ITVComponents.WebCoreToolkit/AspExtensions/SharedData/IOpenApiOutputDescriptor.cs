using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.SharedData
{
    public interface IOpenApiOutputDescriptor
    {
        public bool IsError { get; }
        public int Code { get; set; }

        public string ContentType { get; set; }

        public string Description { get; set; }

        public Type Type { get; set; }
    }
}
