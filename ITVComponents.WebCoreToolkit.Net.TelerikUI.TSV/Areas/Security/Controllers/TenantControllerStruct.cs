using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Tenants.View,Tenants.Write,Tenants.AssignUser,Tenants.WriteSettings,Tenants.AssignNav,Tenants.AssignQuery),HasFeature(ITVAdminViews)"), Area("Security"), ConstructedGenericControllerConvention(ControllerName = "TenantController")]
    public class TenantControllerStruct<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TContext> : Controller
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>, new()
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>, new()
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>, new()
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization: DashboardWidgetLocalization<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TUserId: struct
        where TAssetTemplate : AssetTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser>
    {
        private readonly TContext db;

        private readonly IUserExpressionHelper<TUserId, TUser, TTenantUser> expressionHelper;
        private readonly IOptions<SecurityViewsOptions> options;

        public TenantControllerStruct(TContext db, IUserExpressionHelper<TUserId, TUser, TTenantUser> expressionHelper, IServiceProvider services, IOptions<SecurityViewsOptions> options)
        {
            this.db = db;
            this.expressionHelper = expressionHelper;
            this.options = options;
            if (!services.VerifyUserPermissions(new[] { EntityFramework.TenantSecurityShared.Helpers.ToolkitPermission.Sysadmin}))
            {
                db.HideGlobals = true;
                //isSysAdmin = false;
            }
            else
            {
                db.ShowAllTenants = true;
                //isSysAdmin = true;
            }
        }

        [Authorize("HasPermission(Tenants.View,Tenants.Write)")]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize("HasPermission(Tenants.AssignUser)")]
        public IActionResult TenantTable(TUserId userId)
        {
            return View(userId);
        }

        [Authorize("HasPermission(Tenants.AssignNav)")]
        public IActionResult TenantTableNav(int parentId)
        {
            ViewData["parentId"] = parentId;
            //ViewData["Permissions"] = db.ReadForeignKey("Permissions").Cast<ForeignKeyData<int>>().ToList();
            return PartialView();
        }

        [Authorize("HasPermission(Tenants.AssignQuery)")]
        public IActionResult TenantTableQry(int diagnosticsQueryId)
        {
            ViewData["diagnosticsQueryId"] = diagnosticsQueryId;
            //ViewData["Permissions"] = db.ReadForeignKey("Permissions").Cast<ForeignKeyData<int>>().ToList();
            return PartialView();
        }

        public IActionResult SettingsTable(int tenantId)
        {
            ViewData["tenantId"] = tenantId;
            return PartialView();
        }

        public IActionResult DetailsView(int tenantId)
        {
            return PartialView(tenantId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] TUserId? userId, [FromQuery] int? navigationMenuId, [FromQuery]int? diagnosticsQueryId)
        {
            if (userId == null && navigationMenuId == null && diagnosticsQueryId == null)
            {
                return Json(db.Tenants.ToDataSourceResult(request, n => n.ToViewModel<Tenant, TenantViewModel>()));
            }

            if (userId != null)
            {
                return Json((from p in db.Tenants
                    join r in db.TenantUsers on new { p.TenantId, UserId = userId.Value } equals new
                        { r.TenantId, r.UserId } into lj
                    from s in lj.DefaultIfEmpty()
                    select new TenantViewModel
                    {
                        TenantId = p.TenantId,
                        UserId = userId,
                        Assigned = s != null,
                        UniQUID = $"{p.TenantId}_{userId}",
                        DisplayName = p.DisplayName,
                        TenantName = p.TenantName
                    }).ToDataSourceResult(request, ModelState));
            }

            if (navigationMenuId != null)
            {
                return Json((from p in db.Tenants
                    join n in db.TenantNavigation on new
                            { p.TenantId, NavigationMenuId = navigationMenuId.Value } equals
                        new { n.TenantId, n.NavigationMenuId }
                        into lj
                    from s in lj.DefaultIfEmpty()
                    select new TenantViewModel()
                    {
                        TenantId = p.TenantId,
                        Assigned = s != null,
                        UniQUID = $"N_{p.TenantId}_{navigationMenuId}",
                        DisplayName = p.DisplayName,
                        TenantName = p.TenantName
                    }).ToDataSourceResult(request, ModelState));
            }

            if (diagnosticsQueryId != null)
            {
                return Json((from p in db.Tenants
                    join n in db.TenantDiagnosticsQueries on new
                            { p.TenantId, DiagnosticsQueryId = diagnosticsQueryId.Value } equals
                        new { n.TenantId, n.DiagnosticsQueryId }
                        into lj
                    from s in lj.DefaultIfEmpty()
                    select new TenantViewModel()
                    {
                        TenantId = p.TenantId,
                        Assigned = s != null,
                        UniQUID = $"N_{p.TenantId}_{diagnosticsQueryId}",
                        DisplayName = p.DisplayName,
                        TenantName = p.TenantName
                    }).ToDataSourceResult(request, ModelState));
            }

            return NotFound();
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new Tenant();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantViewModel,Tenant>(model);
                if (options.Value.UseExplicitTenantPasswords)
                {
                    model.TenantPassword = Convert.ToBase64String(AesEncryptor.CreateKey());
                }

                db.Tenants.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<Tenant, TenantViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel)
        {
            var model = db.Tenants.First(n => n.TenantId== viewModel.TenantId);
            if (ModelState.IsValid)
            {
                db.Tenants.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel)
        {
            var model = db.Tenants.First(n => n.TenantId == viewModel.TenantId);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<TenantViewModel, Tenant>(model, "", m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<Tenant, TenantViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.AssignUser)")]
        public async Task<IActionResult> UpdateTU([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel, TUserId userId)
        {
            var user = db.Users.First(expressionHelper.EqualsUserId(userId));
            var model = db.TenantUsers.FirstOrDefault(expressionHelper.EqualsUserTenantId(userId, viewModel.TenantId));
            if ((model == null) == viewModel.Assigned)
            {
                if (model == null)
                {
                    db.TenantUsers.Add(new TTenantUser()
                    {
                        TenantId = viewModel.TenantId,
                        UserId = expressionHelper.UserId(user)
                    });
                }
                else
                {
                    db.TenantUsers.Remove(model);
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.AssignNav)")]
        public async Task<IActionResult> UpdateTN([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel, int navigationMenuId)
        {
            var nav = db.Navigation.First(n => n.NavigationMenuId == navigationMenuId);
            var model = db.TenantNavigation.FirstOrDefault(n => n.NavigationMenuId == nav.NavigationMenuId && n.TenantId == viewModel.TenantId);
            if ((model == null) == viewModel.Assigned)
            {
                if (model == null)
                {
                    db.TenantNavigation.Add(new TTenantNavigation()
                    {
                        TenantId = viewModel.TenantId,
                        NavigationMenu = nav
                    });
                }
                else
                {
                    db.TenantNavigation.Remove(model);
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.AssignQuery)")]
        public async Task<IActionResult> UpdateTQ([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel, int diagnosticsQueryId)
        {
            var qry = db.DiagnosticsQueries.First(n => n.DiagnosticsQueryId == diagnosticsQueryId);
            var model = db.TenantDiagnosticsQueries.FirstOrDefault(n => n.DiagnosticsQueryId == qry.DiagnosticsQueryId && n.TenantId == viewModel.TenantId);
            if ((model == null) == viewModel.Assigned)
            {
                if (model == null)
                {
                    db.TenantDiagnosticsQueries.Add(new TTenantQuery()
                    {
                        TenantId = viewModel.TenantId,
                        DiagnosticsQuery = qry
                    });
                }
                else
                {
                    db.TenantDiagnosticsQueries.Remove(model);
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        public IActionResult ReadSettings([DataSourceRequest] DataSourceRequest request, [FromQuery]int tenantId)
        {
            return Json(db.TenantSettings.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, n => n.ToViewModel<TenantSetting, TenantSettingViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.WriteSettings)")]
        public async Task<IActionResult> CreateSetting([DataSourceRequest] DataSourceRequest request,[FromQuery] int tenantId)
        {
            var model = new TenantSetting();
            if (ModelState.IsValid)
            {
                var tenant = db.Tenants.First(n => n.TenantId == tenantId);
                await this.TryUpdateModelAsync<TenantSettingViewModel, TenantSetting>(model);
                model.TenantId = tenantId;
                if (model.JsonSetting)
                {
                    model.SettingsValue = model.SettingsValue.EncryptJsonValues(tenant.TenantPassword);
                }

                db.TenantSettings.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TenantSetting, TenantSettingViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.WriteSettings)")]
        public async Task<IActionResult> UpdateSetting([DataSourceRequest] DataSourceRequest request, TenantSettingViewModel viewModel)
        {
            var model = db.TenantSettings.First(n => n.TenantSettingId == viewModel.TenantSettingId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantSettingViewModel,TenantSetting>(model, "", m => { return m.ElementType == null; });
                if (model.JsonSetting)
                {
                    model.SettingsValue = model.SettingsValue.EncryptJsonValues(model.Tenant.TenantPassword);
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TenantSetting, TenantSettingViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.WriteSettings)")]
        public async Task<IActionResult> DestroySetting([DataSourceRequest] DataSourceRequest request, TenantSettingViewModel viewModel)
        {
            var model = db.TenantSettings.First(n => n.TenantSettingId == viewModel.TenantSettingId);
            if (ModelState.IsValid)
            {
                db.TenantSettings.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
