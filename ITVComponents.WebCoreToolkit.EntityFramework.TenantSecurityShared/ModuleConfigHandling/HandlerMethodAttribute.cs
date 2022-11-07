using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.ModuleConfigHandling
{
    [AttributeUsage(AttributeTargets.Method)]
    public class HandlerMethodAttribute:Attribute
    {
        public HandlerMethodName Name { get; }

        public HandlerMethodAttribute(HandlerMethodName name)
        {
            Name = name;
        }
    }

    public enum HandlerMethodName
    {
        GetConfig,
        SetConfig,
        GetParameters
    }
}
