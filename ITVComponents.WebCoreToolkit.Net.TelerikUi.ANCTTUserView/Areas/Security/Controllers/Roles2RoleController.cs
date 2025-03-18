using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.Areas.Security.Controllers
{
    [Authorize("HasPermission(Roles.AssignRole,Roles.View),HasFeature(ITVAdminViews)"), Area("Security"), ConstructedGenericControllerConvention]
    public class Roles2RoleController<TContext> : Controller
        where TContext : AspNetTreeSecurityContext<TContext>
    {
        private readonly TContext db;
        private readonly bool isSysAdmin;

        public Roles2RoleController(TContext db, IServiceProvider services)
        {
            this.db = db;
            if (!services.VerifyUserPermissions(new[] { EntityFramework.TenantSecurityShared.Helpers.ToolkitPermission.Sysadmin }))
            {
                db.HideGlobals = true;
                isSysAdmin = false;
            }
            else
            {
                db.ShowAllTenants = true;
                isSysAdmin = true;
            }
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, int tenantId, [FromQuery] int roleId)
        {
            IDisposable trustDisposable = null;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
                var trust = new HierarchyTenantContextSecurityTrustConfig
                {
                    IncludeParentTree = true
                };
                trustDisposable = FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>.CreateForCaller(
                    db, db,
                    trust);
            }

            int[] tenants = [tenantId];
            var tenant = db.Tenants.Include(n => n.ParentTenant).First(n => n.TenantId == tenantId);
            string parentName = null;
            if (tenant.ParentTenantId != null)
            {
                parentName = tenant.ParentTenant.DisplayName;
                tenants = [.. tenants, tenant.ParentTenantId.Value];
            }

            try
            {
                return Json((from p in db.SecurityRoles
                        join r in db.RoleRoles /*.Where(n => n.PermissiveRoleId != null && n.PermittedRoleId != null)*/
                            on new { p.RoleId, PermissiveRoleId = roleId } equals new
                            {
                                RoleId = r.PermittedRoleId.Value, PermissiveRoleId = r.PermissiveRoleId.Value
                            } into lj
                        from s in lj.DefaultIfEmpty()
                        where tenants.Contains(p.TenantId)
                        select new RoleViewModel
                        {
                            RoleId = p.RoleId,
                            PermissiveRoleId = roleId,
                            RoleName = p.TenantId == tenantId?p.RoleName:$"{parentName}\\{p.RoleName}",
                            Assigned = s != null,
                            TenantId = p.TenantId,
                            UniQUID = $"{p.RoleId}_{roleId}"
                        }).ToArray().Where(n => !db.IsCyclicRoleInheritance(roleId, n.RoleId))
                    .ToDataSourceResult(request, ModelState));
            }
            finally
            {
                trustDisposable?.Dispose();
            }
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.AssignRole)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, RoleViewModel viewModel)
        {
            IDisposable trustDisposable = null;
            if (!isSysAdmin)
            {
                var trust = new HierarchyTenantContextSecurityTrustConfig
                {
                    IncludeParentTree = true
                };
                trustDisposable = FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>.CreateForCaller(
                    db, db,
                    trust);
            }

            try
            {
                if (viewModel.PermissiveRoleId != null &&
                    HttpContext.RequestServices.VerifyUserPermissions(new[] { "Roles.AssignRole" }))
                {
                    var model = db.RoleRoles.FirstOrDefault(n =>
                        n.PermissiveRoleId == viewModel.PermissiveRoleId && n.PermittedRoleId == viewModel.RoleId &&
                        (n.PermissiveRole.TenantId == viewModel.TenantId || n.PermissiveRole.Tenant.ParentTenantId == viewModel.TenantId));
                    var role = db.SecurityRoles.First(n =>
                        n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                    if ((model == null) == viewModel.Assigned)
                    {
                        if (model == null)
                        {
                            db.RoleRoles.Add(new RoleRole()
                            {
                                PermissiveRoleId = viewModel.PermissiveRoleId.Value,
                                PermittedRoleId = viewModel.RoleId
                            });
                        }
                        else
                        {
                            db.RoleRoles.Remove(model);
                        }

                        await db.SaveChangesAsync();
                    }

                    return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
                }
            }
            finally
            {
                trustDisposable?.Dispose();
            }

            return Unauthorized();
        }
    }
}
