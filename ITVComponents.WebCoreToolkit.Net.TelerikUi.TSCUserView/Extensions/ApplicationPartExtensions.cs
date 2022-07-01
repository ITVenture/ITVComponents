using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvUserView(this ApplicationPartManager manager)
        {
            var dic = new Dictionary<string, Type>
            {
                { "TContext", typeof(SecurityContext)}
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }

        public static ApplicationPartManager EnableItvUserView<TContext>(this ApplicationPartManager manager)
            where TContext : SecurityContext<TContext>
        {
            var dic = new Dictionary<string, Type>
            {
                { "TContext", typeof(TContext)}
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }

        public static ApplicationPartManager EnableItvUserView(this ApplicationPartManager manager, Type contextType)
        {
            if (!typeof(DbContext).IsAssignableFrom(contextType))
            {
                throw new InvalidOperationException("contextType must implement DbContext");
            }

            var dic = new Dictionary<string, Type>
            {
                { "TContext", contextType}
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }
    }
}
