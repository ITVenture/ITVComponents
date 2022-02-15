using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Resources;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TextsAndMessagesHelper = ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Resources.TextsAndMessagesHelper;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Permissions.Write,Permissions.Assign,Permissions.View)"), Area("Security"), ConstructedGenericControllerConvention]
    public class PermissionController<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TContext> : Controller
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>, new()
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>, new()
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty>
    {
        private readonly TContext db;

        private bool isSysAdmin;

        public PermissionController(TContext db, IServiceProvider services)
        {
            this.db = db;
            if (!services.VerifyUserPermissions(new[] { EntityFramework.TenantSecurityShared.Helpers.ToolkitPermission.Sysadmin}))
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

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult PermissionTable(int? tenantId, int? roleId)
        {
            if (roleId != null && tenantId == null && isSysAdmin)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_PC_Tenant_And_Role_Required);
            }

            ViewData["tenantId"] = tenantId;
            ViewData["roleId"] = roleId;
            return PartialView();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, [FromQuery]int? tenantId, [FromQuery]int? roleId)
        {
            if (roleId == null)
            {
                if (isSysAdmin)
                {
                    return Json(db.Permissions.Where(n => n.TenantId == null || n.TenantId == tenantId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TPermission, PermissionViewModel>((m, v) =>
                    {
                        v.Editable = m.TenantId == tenantId;
                        v.TenantId = tenantId;
                    })));
                }
                else
                {
                    db.HideGlobals = true;
                    return Json(db.Permissions.ToDataSourceResult(request, ModelState, n => n.ToViewModel<TPermission, PermissionViewModel>((m, v) => v.Editable = true)));
                }
            }
            else
            {
                var role = db.SecurityRoles.First(n => n.RoleId == roleId.Value);
                if (tenantId == null && !isSysAdmin)
                {
                    tenantId = db.CurrentTenantId;
                }
                else if (tenantId == null)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_PC_Tenant_And_Role_Required);
                }

                if (role.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_PC_Only_Sysadmin_Can_View_and_Modify_System_Role_Perms);
                }

                return Json((from p in db.Permissions
                    join r in db.RolePermissions on new {p.PermissionId, TenantId = p.TenantId ?? tenantId.Value, RoleId = roleId.Value} equals new {r.PermissionId, r.TenantId, r.RoleId} into lj
                    from s in lj.DefaultIfEmpty()
                    where (p.TenantId == null && isSysAdmin) || p.TenantId == tenantId
                    select new PermissionViewModel
                    {
                        PermissionId = p.PermissionId,
                        PermissionName = p.PermissionName,
                        TenantId = tenantId,
                        RoleId = roleId,
                        Assigned = s != null,
                        IsGlobal = p.TenantId == null,
                        //IsGlobal = p.IsGlobal,
                        UniQUID = $"{p.PermissionId}_{tenantId}_{roleId}"
                    }).ToDataSourceResult(request, ModelState));
            }
        }

        [HttpPost]
        [Authorize("HasPermission(Permissions.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, [FromQuery]int? tenantId)
        {
            if (!isSysAdmin && tenantId == null)
            {
                tenantId = db.CurrentTenantId;
            }

            if (!isSysAdmin && tenantId == null)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_P_Tenant_Req_For_Non_Admins);
            }
            
            var model = new TPermission();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<PermissionViewModel,TPermission>(model);
                if (tenantId == null || UnknownGlobalPermission(model.PermissionName))
                {
                    model.TenantId = tenantId;
                    db.Permissions.Add(model);
                    await db.SaveChangesAsync();
                }
                else
                {
                    ModelState.AddModelError("Error",TextsAndMessagesHelper.IWCN_P_Illegal_Permission_Override);
                }
            }

            return Json(await new[] {model.ToViewModel<TPermission, PermissionViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Permissions.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, PermissionViewModel viewModel)
        {
            var model = db.Permissions.First(n => n.PermissionId== viewModel.PermissionId);
            if (model.TenantId != null || isSysAdmin)
            {
                if (ModelState.IsValid)
                {
                    db.Permissions.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
            }

            return BadRequest(TextsAndMessagesHelper.IWCN_P_Sysadmin_Req_To_Del_Global_Perm);
        }

        [HttpPost]
        [Authorize("HasPermission(Permissions.Write,Permissions.Assign)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, PermissionViewModel viewModel)
        {
            if ((viewModel.TenantId == null && isSysAdmin || viewModel.RoleId == null) && HttpContext.RequestServices.VerifyUserPermissions(new []{"Permissions.Write"}))
            {
                var model = db.Permissions.First(n => n.PermissionId == viewModel.PermissionId && n.TenantId == viewModel.TenantId);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<PermissionViewModel,TPermission>(model, "", m => { return m.ElementType == null; });
                    if (model.TenantId == null || UnknownGlobalPermission(model.PermissionName))
                    {
                        await db.SaveChangesAsync();
                    }
                    else
                    {
                        ModelState.AddModelError("Error",TextsAndMessagesHelper.IWCN_P_Illegal_Permission_Override);
                    }
                }

                return Json(await new[] {model.ToViewModel<TPermission, PermissionViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }
            else if (HttpContext.RequestServices.VerifyUserPermissions(new []{"Permissions.Assign"}))
            {
                var tenantId = viewModel.TenantId;
                if (!isSysAdmin)
                {
                    tenantId = db.CurrentTenantId;
                }

                var role = db.SecurityRoles.First(n => n.RoleId == viewModel.RoleId.Value);

                if (tenantId != null && !role.IsSystemRole || isSysAdmin)
                {
                    var model = db.RolePermissions.FirstOrDefault(n => n.PermissionId == viewModel.PermissionId && n.RoleId == viewModel.RoleId && n.TenantId == tenantId.Value);
                    if ((model == null) == viewModel.Assigned)
                    {
                        if (model == null)
                        {
                            db.RolePermissions.Add(new TRolePermission
                            {
                                PermissionId = viewModel.PermissionId,
                                RoleId = viewModel.RoleId.Value,
                                TenantId = tenantId.Value
                            });
                        }
                        else
                        {
                            db.RolePermissions.Remove(model);
                        }

                        await db.SaveChangesAsync();
                    }

                    return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
                }
            }

            return Unauthorized();
        }
        
        private bool UnknownGlobalPermission(string name)
        {
            var allT = db.ShowAllTenants;
            var hideG = db.HideGlobals;
            try
            {
                db.ShowAllTenants = true;
                db.HideGlobals = true;
                return !db.Permissions.Any(n => n.PermissionName == name && n.TenantId == null);
            }
            finally
            {
                db.ShowAllTenants = allT;
                db.HideGlobals = hideG;
            }
        }
    }
}
