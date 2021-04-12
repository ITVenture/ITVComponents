using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Navigation
{
    internal class DbNavigationBuilder:INavigationBuilder
    {
        private readonly SecurityContext securityContext;
        private readonly IServiceProvider services;
        private readonly IPermissionScope permissionScope;

        public DbNavigationBuilder(SecurityContext securityContext, IServiceProvider services, IPermissionScope permissionScope)
        {
            this.securityContext = securityContext;
            this.services = services;
            this.permissionScope = permissionScope;
        }

        public NavigationMenu GetNavigationRoot()
        {
            using var tmp = new FullSecurityAccessHelper(securityContext, false, false);
            string explicitTenant = null;
            if (permissionScope.IsScopeExplicit)
            {
                explicitTenant = permissionScope.PermissionPrefix;
            }
            
            NavigationMenu retVal = new NavigationMenu();
            retVal.Children.AddRange(SelectNavigation(null, explicitTenant));
            return retVal;
        }

        private IEnumerable<NavigationMenu> SelectNavigation(int? parent, string explicitTenant)
        {
            var items = from n in securityContext.Navigation where n.ParentId == parent orderby n.SortOrder??0 select n;
            foreach (var item in items)
            {
                NavigationMenu ret = new NavigationMenu
                {
                    DisplayName = item.DisplayName,
                    RequiredPermission = item.EntryPoint?.PermissionName,
                    SortOrder = item.SortOrder ?? 0,
                    SpanClass = item.SpanClass,
                    Url = !string.IsNullOrEmpty(item.Url) ? $"{(!string.IsNullOrEmpty(explicitTenant) ? $"/{explicitTenant}" : "")}{(!item.Url.StartsWith("/") ? "/" : "")}{item.Url}" : ""
                };
                
                if (string.IsNullOrEmpty(ret.RequiredPermission) || services.VerifyUserPermissions(new[] {ret.RequiredPermission}))
                {
                    if (string.IsNullOrEmpty(item.Url))
                    {
                        ret.Children.AddRange(SelectNavigation(item.NavigationMenuId,explicitTenant));
                    }

                    yield return ret;
                }
            }
        }
    }
}
