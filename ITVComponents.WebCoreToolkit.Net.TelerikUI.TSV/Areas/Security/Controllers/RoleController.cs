using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
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
    [Authorize("HasPermission(Roles.Write,Roles.AssignPermission,Roles.AssignRole,Roles.AssignUser,Roles.View),HasFeature(ITVAdminViews)"), Area("Security"), ConstructedGenericControllerConvention]
    public class RoleController<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TContext> : Controller
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>, new()
    {
       private readonly TContext db;
       private bool isSysAdmin;

        public RoleController(TContext db, IServiceProvider services)
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
            if (db.CurrentTenantId == null)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_RC_Tenant_Bound_User_Required_For_Index);
            }
            
            return View(db.CurrentTenantId.Value);
        }

        public IActionResult RoleTable(int tenantId, int? permissionId, int? tenantUserId, int? roleId)
        {
            ViewData["tenantId"] = tenantId;
            ViewData["permissionId"] = permissionId;
            ViewData["tenantUserId"] = tenantUserId;
            ViewData["roleId"] = roleId;
            return PartialView();
        }

        public IActionResult RoleDetails(int tenantId, int roleId)
        {
            var tmp = db.SecurityRoles.First(n => n.RoleId == roleId && n.TenantId == tenantId);
            return View(tmp.ToViewModel<TRole, RoleViewModel>());
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, int tenantId, [FromQuery]int? permissionId, [FromQuery]int? tenantUserId, [FromQuery]int? roleId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
            }
            
            if (tenantUserId == null && permissionId == null && roleId == null)
            {
                return Json(db.SecurityRoles.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, n => n.ToViewModel<TRole, RoleViewModel>((m,v) => v.Editable = !m.IsSystemRole || isSysAdmin)));
            }
            else if (permissionId != null)
            {
                return Json((from p in db.SecurityRoles
                    join r in db.RolePermissions/*.Where(n => n.RoleId != null)*/ on new {p.RoleId, p.TenantId, PermissionId = permissionId.Value} equals new {RoleId=r.RoleId.Value, r.TenantId, r.PermissionId} into lj
                    from s in lj.DefaultIfEmpty()
                    where p.TenantId == tenantId && (!p.IsSystemRole || isSysAdmin)
                    select new RoleViewModel
                    {
                        RoleId = p.RoleId,
                        TenantId = tenantId,
                        PermissionId = permissionId,
                        Assigned = s != null,
                        UniQUID = $"{p.RoleId}_{tenantId}_{permissionId}",
                        RoleName=p.RoleName
                    }).ToDataSourceResult(request, ModelState));
            }
            else if (tenantUserId != null)
            {
                return Json((from p in db.SecurityRoles
                    join r in db.TenantUserRoles/*.Where(n => n.TenantUserId != null && n.RoleId != null)*/ on new {p.RoleId, TenantUserId = tenantUserId.Value} equals new {RoleId=r.RoleId.Value, TenantUserId=r.TenantUserId.Value} into lj
                    from s in lj.DefaultIfEmpty()
                    where p.TenantId == tenantId
                    select new RoleViewModel
                    {
                        RoleId = p.RoleId,
                        UserId = tenantUserId,
                        RoleName = p.RoleName,
                        Assigned = s != null,
                        TenantId = tenantId,
                        UniQUID = $"{p.RoleId}_{tenantUserId}"
                    }).ToDataSourceResult(request, ModelState));
            }
            else
            {
                return Json((from p in db.SecurityRoles
                    join r in db.RoleRoles/*.Where(n => n.PermissiveRoleId != null && n.PermittedRoleId != null)*/ on new { p.RoleId, PermissiveRoleId = roleId.Value } equals new { RoleId=r.PermittedRoleId.Value, PermissiveRoleId=r.PermissiveRoleId.Value } into lj
                    from s in lj.DefaultIfEmpty()
                    where p.TenantId == tenantId
                    select new RoleViewModel
                    {
                        RoleId = p.RoleId,
                        PermissiveRoleId = roleId,
                        RoleName = p.RoleName,
                        Assigned = s != null,
                        TenantId = tenantId,
                        UniQUID = $"{p.RoleId}_{roleId}"
                    }).ToArray().Where(n => !db.IsCyclicRoleInheritance(roleId.Value, n.RoleId)).ToDataSourceResult(request, ModelState));
            }
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,[FromQuery] int tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
            }
            var model = new TRole();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<RoleViewModel,TRole>(model);
                if (model.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
                }
                
                model.TenantId = tenantId;
                db.SecurityRoles.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TRole, RoleViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, RoleViewModel viewModel)
        {
            var model = db.SecurityRoles.First(n => n.RoleId== viewModel.RoleId);
            if (model.IsSystemRole && !isSysAdmin)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
            }
            
            if (ModelState.IsValid)
            {
                db.SecurityRoles.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.Write,Roles.AssignPermission,Roles.AssignUser)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, RoleViewModel viewModel)
        {
            if (viewModel.PermissionId == null && viewModel.UserId == null &&
                viewModel.PermissiveRoleId == null &&
                HttpContext.RequestServices.VerifyUserPermissions(new[] { "Roles.Write" }))
            {
                var model = db.SecurityRoles.First(
                    n => n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if (model.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
                }

                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<RoleViewModel, TRole>(model, "",
                        m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(
                    await new[] { model.ToViewModel<TRole, RoleViewModel>() }.ToDataSourceResultAsync(request,
                        ModelState));
            }
            else if (viewModel.PermissionId != null &&
                     HttpContext.RequestServices.VerifyUserPermissions(new[] { "Roles.AssignPermission" }))
            {
                var role = db.SecurityRoles.First(n =>
                    n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if (role.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
                }

                var model = db.RolePermissions.FirstOrDefault(n =>
                    n.PermissionId == viewModel.PermissionId && n.RoleId == viewModel.RoleId &&
                    n.TenantId == viewModel.TenantId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.RolePermissions.Add(new TRolePermission
                        {
                            PermissionId = viewModel.PermissionId.Value,
                            RoleId = viewModel.RoleId,
                            TenantId = viewModel.TenantId
                        });
                    }
                    else
                    {
                        db.RolePermissions.Remove(model);
                    }

                    await db.SaveChangesAsync();
                }

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.UserId != null &&
                     HttpContext.RequestServices.VerifyUserPermissions(new[] { "Roles.AssignUser" }))
            {
                var model = db.TenantUserRoles.FirstOrDefault(n =>
                    n.TenantUserId == viewModel.UserId && n.RoleId == viewModel.RoleId &&
                    n.User.TenantId == viewModel.TenantId);
                var role = db.SecurityRoles.First(n =>
                    n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.TenantUserRoles.Add(new TUserRole
                        {
                            TenantUserId = viewModel.UserId.Value,
                            RoleId = viewModel.RoleId
                        });
                    }
                    else
                    {
                        db.TenantUserRoles.Remove(model);
                    }

                    await db.SaveChangesAsync();
                }

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.PermissiveRoleId != null &&
                     HttpContext.RequestServices.VerifyUserPermissions(new[] { "Roles.AssignRole" }))
            {
                var model = db.RoleRoles.FirstOrDefault(n =>
                    n.PermissiveRoleId == viewModel.PermissiveRoleId && n.PermittedRoleId == viewModel.RoleId &&
                    n.PermissiveRole.TenantId == viewModel.TenantId);
                var role = db.SecurityRoles.First(n =>
                    n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.RoleRoles.Add(new TRoleRole()
                        {
                            PermissiveRoleId= viewModel.PermissiveRoleId.Value,
                            PermittedRoleId= viewModel.RoleId
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

            return Unauthorized();
        }
    }
}
