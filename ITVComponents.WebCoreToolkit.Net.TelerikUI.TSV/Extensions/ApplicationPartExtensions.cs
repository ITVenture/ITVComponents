using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvTenantViews<TContext>(this ApplicationPartManager manager) where TContext:DbContext
        {
            var dic = typeof(TContext).GetSecurityContextArguments();
            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }

        public static ApplicationPartManager EnableItvTenantViews(this ApplicationPartManager manager, Type contextType)
        {
            if (!typeof(DbContext).IsAssignableFrom(contextType))
            {
                throw new InvalidOperationException("contextType must implement DbContext");
            }

            var dic = contextType.GetSecurityContextArguments();

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
