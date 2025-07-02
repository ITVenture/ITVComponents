using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
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
    [Authorize("HasPermission(GlobalRoles.Write,GlobalRoles.AssignPermission,GlobalRoles.AssignRole,Sysadmin,GlobalRoles.View),HasFeature(ITVAdminViews)"), Area("Security"), ConstructedGenericControllerConvention]
    public class GlobalRoleController<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig, TContext> : Controller
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig>
        where TTenant : Tenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TTrustConfig : BaseTenantContextSecurityTrustConfig, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    {
       private readonly TContext db;
       private bool isSysAdmin;

        public GlobalRoleController(TContext db, IServiceProvider services)
        {
            this.db = db;
            db.HideGlobals = false;
        }
        
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult PermissionList(int globalRoleId)
        {
            var globalRole = db.GlobalRoles.FirstOrDefault(n => n.GlobalRoleId == globalRoleId).ToViewModel<TGlobalRole,GlobalRoleViewModel>();
            return PartialView(globalRole);
        }

        public IActionResult GlobalRoleToRoleList(int roleId)
        {
            ViewData["RoleId"] = roleId;
            return PartialView();
        }

        #region Default-CRUD
        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, [FromQuery] int? roleId)
        {
            if (roleId == null)
            {
                return Json(db.GlobalRoles.ToDataSourceResult(request,
                    n => n.ToViewModel<TGlobalRole, GlobalRoleViewModel>()));
            }

            return Json((from t in db.GlobalRoles
                join rl in db.GlobalToLocalRoles.Where(n => n.LocalRoleId == roleId && n.RoleRoleId == null && n.OriginId == null) on t.GlobalRoleId equals rl
                    .GlobalRoleId into rlj
                from lj in rlj.DefaultIfEmpty()
                select new { GlobalRole = t, IsAssigned = lj != null }).ToDataSourceResult(request.RemapRequestMembers(cl =>
                {
                    switch (cl.ToLower())
                    {
                        case "rolename":
                            return "GlobalRole.RoleName";
                        case "assigned":
                            return "IsAssigned";
                        case "roledescription":
                            return "GlobalRole.RoleDescription";
                    }

                    return cl;
                }),
                n => new GlobalRoleViewModel
                {
                    RoleName = n.GlobalRole.RoleName,
                    RoleId = roleId,
                    GlobalRoleId = n.GlobalRole.GlobalRoleId,
                    Assigned = n.IsAssigned,
                    UniQUID = $"G2L{roleId}__{n.GlobalRole.GlobalRoleId}"
                }));
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalRoles.Write,Sysadmin)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new TGlobalRole();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<GlobalRoleViewModel,TGlobalRole>(model);
                db.GlobalRoles.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TGlobalRole, GlobalRoleViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalRoles.Write,Sysadmin)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, GlobalRoleViewModel viewModel)
        {
            var model = db.GlobalRoles.First(n => n.GlobalRoleId == viewModel.GlobalRoleId);
            if (ModelState.IsValid)
            {
                db.GlobalRoles.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalRoles.Write,Sysadmin)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, GlobalRoleViewModel viewModel)
        {
            if (viewModel.RoleId == null &&
                HttpContext.RequestServices.VerifyUserPermissions(new[] { "GlobalRoles.Write" }))
            {
                var model = db.GlobalRoles.First(
                    n => n.GlobalRoleId == viewModel.GlobalRoleId);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<GlobalRoleViewModel, TGlobalRole>(model, "",
                        m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(
                    await new[] { model.ToViewModel<TGlobalRole, GlobalRoleViewModel>() }.ToDataSourceResultAsync(request,
                        ModelState));
            }

            return Forbid();
        }
        #endregion    

        [HttpPost]
        public IActionResult ReadGlobalPermissions([DataSourceRequest] DataSourceRequest request, [FromQuery]int globalRoleId)
        {
            if (globalRoleId != 0)
            {
                return Json((from p in db.Permissions.Where(n => n.TenantId == null)
                    join t in db.GlobalRolePermissions.Where(rp => rp.GlobalRoleId == globalRoleId) on p.PermissionId
                        equals t.PermissionId into tmpJ
                    from j in tmpJ.DefaultIfEmpty()
                    select new { Permission = p, IsAssigned = j != null }).ToDataSourceResult(
                    request.RemapRequestMembers(cl =>
                    {
                        switch (cl.ToLower())
                        {
                            case "permissionname":
                                return "Permission.PermissionName";
                            case "assigned":
                                return "IsAssigned";
                            case "description":
                                return "Permission.Description";
                        }

                        return cl;
                    }),
                    n => new PermissionViewModel
                    {
                        Assigned = n.IsAssigned,
                        RoleId = globalRoleId,
                        PermissionId = n.Permission.PermissionId,
                        Description = n.Permission.Description,
                        PermissionName = n.Permission.PermissionName,
                        UniQUID = $"GR2P{globalRoleId}__{n.Permission.PermissionId}"
                    }));
            }

            return Json(new DataSourceResult { Data = Array.Empty<PermissionViewModel>(), Total = 0 });
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalRoles.AssignPermission,Sysadmin)")]
        public async Task<IActionResult> UpdateGlobalPermission([DataSourceRequest] DataSourceRequest request, PermissionViewModel viewModel)
        {
            if (viewModel.RoleId != null)
            {
                var model = db.GlobalRolePermissions.FirstOrDefault(
                    n => n.GlobalRoleId == viewModel.RoleId && n.PermissionId == viewModel.PermissionId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model != null)
                    {
                        db.GlobalRolePermissions.Remove(model);
                    }
                    else
                    {
                        model = new TGlobalRolePermission
                        {
                            GlobalRoleId = viewModel.RoleId.Value,
                            PermissionId = viewModel.PermissionId
                        };
                        db.GlobalRolePermissions.Add(model);
                    }
                        
                    await db.SaveChangesAsync();
                }

                return Json(
                    await new[] { viewModel }.ToDataSourceResultAsync(request,
                        ModelState));
            }

            return Forbid();
        }


        [HttpPost]
        [Authorize("HasPermission(GlobalRoles.AssignRole,Sysadmin)")]
        public async Task<IActionResult> UpdateGlobalToLocalRole([DataSourceRequest] DataSourceRequest request, GlobalRoleViewModel viewModel)
        {
            if (viewModel.RoleId != null)
            {
                var model = db.GlobalToLocalRoles.FirstOrDefault(
                    n => n.GlobalRoleId == viewModel.GlobalRoleId && n.LocalRoleId == viewModel.RoleId && n.OriginId == null && n.RoleRoleId == null);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model != null)
                    {
                        db.GlobalToLocalRoles.Remove(model);
                    }
                    else
                    {
                        model = new TGRoleLRole()
                        {
                            GlobalRoleId = viewModel.GlobalRoleId,
                            LocalRoleId = viewModel.RoleId.Value
                        };
                        
                        db.GlobalToLocalRoles.Add(model);
                    }

                    await db.SaveChangesAsync();
                }

                return Json(
                    await new[] { viewModel }.ToDataSourceResultAsync(request,
                        ModelState));
            }

            return Forbid();
        }
    }
}
