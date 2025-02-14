using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.AspExtensions.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class FallbackPageHandlerAttribute:Attribute
    {
        public FallbackPageHandlerAttribute(Type fallbackType)
        {
            FallbackType = fallbackType;
        }

        public Type FallbackType { get; }
    }
}
