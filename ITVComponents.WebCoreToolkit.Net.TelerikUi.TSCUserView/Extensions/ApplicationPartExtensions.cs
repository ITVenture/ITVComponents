using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvUserView(this ApplicationPartManager manager)
        {
            AssemblyPart part = new AssemblyPart(typeof(ApplicationPartExtensions).Assembly);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
