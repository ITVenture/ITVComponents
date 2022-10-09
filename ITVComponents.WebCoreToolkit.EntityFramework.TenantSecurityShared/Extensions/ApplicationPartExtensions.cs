using Microsoft.AspNetCore.Mvc.ApplicationParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager RegisterSharedViews(this ApplicationPartManager manager)
        {
            ApplicationPart part = new AssemblyPart(typeof(ApplicationPartExtensions).Assembly);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
