using ITVComponents.EFRepo.DIIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.BinderModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.SqlServer.BinderContext
{
    public class ProcConfigurationForPartialContext<TContext> : DbModelBuilderOptionsProvider<TContext> where TContext : Microsoft.EntityFrameworkCore.DbContext
    {
        protected override void Configure(DbContextModelBuilderOptions<TContext> options)
        {
            options.ConfigureMethod(GlobalDbObjectNaming.ChildTenantsWithProc, (DbContext c, string userId, string currentTenant, string[] requiredPermissions) =>
            {
                var tmpRet = c.Set<DownwardsUserPermissionView<string>>()
                    .Join(c.Set<BinderTenant>(), l => l.ChildTenantId, r => r.TenantId, (l, r) => new { Left = l, Right = r })
                    .Where(n => n.Left.UserId == userId && n.Left.ViewpointTenantName == currentTenant)
                    .GroupBy(n => new { n.Left.UserId, n.Left.TopmostParentLevel, n.Left.ChildTenantName, n.Left.ChildTenantId, n.Right })
                    .OrderBy(g => g.Key.ChildTenantName).ThenBy(g => g.Key.TopmostParentLevel)
                    .Select(g => new { g.Key.ChildTenantName, g.Key.ChildTenantId, g.Key.UserId, HasRequiredPermissions = g.Any(p => requiredPermissions.Contains(p.Left.PermissionName)), Tenant = g.Key.Right });
                var st = new List<string>();
                var retVal = new List<BinderTenant>();
                foreach (var tmp in tmpRet)
                {
                    var r = !st.Contains(tmp.ChildTenantName);
                    if (!r)
                    {
                        st.Add(tmp.ChildTenantName);
                    }

                    if (r && tmp.HasRequiredPermissions)
                    {
                        retVal.Add(tmp.Tenant);
                    }
                }

                return retVal.AsQueryable();
            });
        }
    }
}
