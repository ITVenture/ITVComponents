using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Areas.Security.Controllers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Options;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvTenantViews<TContext>(this ApplicationPartManager manager, Dictionary<string,Type> customTypeArgs, AssemblyPartTypeLoadBehaviorOptions loadingOptions) where TContext:DbContext
        {
            var dic = typeof(TContext).GetSecurityContextArguments();
            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic, customTypeArgs, defaultBehavior:loadingOptions.DefaultBehavior, customBehaviors:loadingOptions.CustomLoadings);
            manager.ApplicationParts.Add(part);
            return manager;
        }

        public static ApplicationPartManager EnableItvTenantViews(this ApplicationPartManager manager, Type contextType, Dictionary<string, Type> customTypeArgs, AssemblyPartTypeLoadBehaviorOptions loadingOptions)
        {
            if (!typeof(DbContext).IsAssignableFrom(contextType))
            {
                throw new InvalidOperationException("contextType must implement DbContext");
            }

            var dic = contextType.GetSecurityContextArguments();

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic, customTypeArgs, defaultBehavior: loadingOptions.DefaultBehavior, customBehaviors: loadingOptions.CustomLoadings);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
