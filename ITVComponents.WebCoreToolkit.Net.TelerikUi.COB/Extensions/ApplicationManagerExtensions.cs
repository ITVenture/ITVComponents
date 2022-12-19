using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Extensions
{
    public static class ApplicationPartExtensions
    {
        public static ApplicationPartManager EnableItvIdentityViews<TContext>(this ApplicationPartManager manager) where TContext : DbContext, ISecurityContextWithOnboarding
        {
            var dic = new Dictionary<string, Type>
            {
                { "TContext", typeof(TContext) }
            };

            AssemblyPartWithGenerics part = new AssemblyPartWithGenerics(typeof(ApplicationPartExtensions).Assembly, dic);
            manager.ApplicationParts.Add(part);
            return manager;
        }

        public static ApplicationPartManager EnableItvIdentityViews(this ApplicationPartManager manager, Type contextType)
        {
            if (!typeof(DbContext).IsAssignableFrom(contextType) || contextType.GetInterfaces().All(t => t != typeof(ISecurityContextWithOnboarding)))
            {
                throw new InvalidOperationException("contextType must implement DbContext and ISecurityContextWithOnboarding");
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
