using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Sysadmin),HasFeature(ITVAdminViews)"), Area("Security"), ConstructedGenericControllerConvention]
    public class AssetTemplateController<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TContext> : Controller
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>, new()
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>, new()
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
        where TAssetTemplate : AssetTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TAssetTemplatePath : AssetTemplatePath<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TAssetTemplateGrant : AssetTemplateGrant<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TAssetTemplateFeature : AssetTemplateFeature<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>, new()
        where TSharedAsset : SharedAsset<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    {
        private readonly TContext db;

        public AssetTemplateController(TContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult _TemplateDetailTab(int assetTemplateId)
        {
            var mod = db.AssetTemplates.First(n => n.AssetTemplateId == assetTemplateId)
                .ToViewModel<TAssetTemplate, AssetTemplateViewModel>();
            return PartialView(mod);
        }

        public IActionResult _PathTable(int assetTemplateId)
        {
            var mod = db.AssetTemplates.First(n => n.AssetTemplateId == assetTemplateId)
                .ToViewModel<TAssetTemplate, AssetTemplateViewModel>();
            return PartialView(mod);
        }

        public IActionResult _FeatureTable(int assetTemplateId)
        {
            var mod = db.AssetTemplates.First(n => n.AssetTemplateId == assetTemplateId)
                .ToViewModel<TAssetTemplate, AssetTemplateViewModel>();
            return PartialView(mod);
        }

        public IActionResult _PermissionTable(int assetTemplateId)
        {
            var mod = db.AssetTemplates.First(n => n.AssetTemplateId == assetTemplateId)
                .ToViewModel<TAssetTemplate, AssetTemplateViewModel>();
            return PartialView(mod);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.AssetTemplates.ToDataSourceResult(request, n => n.ToViewModel<TAssetTemplate, AssetTemplateViewModel>()));
        }

        [HttpPost]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new TAssetTemplate();
            if (ModelState.IsValid)
            {

                await this.TryUpdateModelAsync<AssetTemplateViewModel, TAssetTemplate>(model);
                db.AssetTemplates.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TAssetTemplate, AssetTemplateViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, AssetTemplateViewModel viewModel)
        {
            var model = db.AssetTemplates.First(n => n.AssetTemplateId == viewModel.AssetTemplateId);
            if (ModelState.IsValid)
            {
                db.AssetTemplates.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, AssetTemplateViewModel viewModel)
        {
            var model = db.AssetTemplates.First(n => n.AssetTemplateId == viewModel.AssetTemplateId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<AssetTemplateViewModel, TAssetTemplate>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TAssetTemplate, AssetTemplateViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> ReadPermissions([DataSourceRequest] DataSourceRequest request,
            [FromQuery] int assetTemplateId)
        {
            return Json(await (from p in db.Permissions
                join r in db.AssetTemplateGrants on new { p.PermissionId, AssetTemplateId = assetTemplateId } equals new { r.PermissionId, r.AssetTemplateId } into lj
                from s in lj.DefaultIfEmpty()
                where p.TenantId == null
                select new PermissionViewModel
                {
                    PermissionId = p.PermissionId,
                    RoleId = assetTemplateId,
                    PermissionName  = p.PermissionName,
                    Description = p.Description,
                    Assigned = s != null,
                    UniQUID = $"P{p.PermissionId}_{assetTemplateId}"
                }).ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePermission([DataSourceRequest] DataSourceRequest request,
            PermissionViewModel viewModel)
        {
            var perm = db.Permissions.First(n => n.PermissionId== viewModel.PermissionId&& n.TenantId == null);
            var model = db.AssetTemplateGrants.FirstOrDefault(n => n.PermissionId == perm.PermissionId && n.AssetTemplateId == viewModel.RoleId);
            if ((model == null) == viewModel.Assigned)
            {
                if (model == null)
                {
                    db.AssetTemplateGrants.Add(new TAssetTemplateGrant()
                    {
                        Permission = perm,
                        AssetTemplateId = viewModel.RoleId.Value
                    });
                }
                else
                {
                    db.AssetTemplateGrants.Remove(model);
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> ReadFeatures([DataSourceRequest] DataSourceRequest request,
            [FromQuery] int assetTemplateId)
        {
            return Json(await (from p in db.Features
                               join r in db.AssetTemplateFeatures on new { p.FeatureId, AssetTemplateId = assetTemplateId } equals new { r.FeatureId, r.AssetTemplateId } into lj
                               from s in lj.DefaultIfEmpty()
                               select new AssetFeatureViewModel()
                               {
                                   FeatureId  = p.FeatureId,
                                   AssetTemplateId = assetTemplateId,
                                   FeatureName = p.FeatureName,
                                   Description = p.FeatureDescription,
                                   Assigned = s != null,
                                   UniQUID = $"F{p.FeatureId}_{assetTemplateId}"
                               }).ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateFeature([DataSourceRequest] DataSourceRequest request,
            AssetFeatureViewModel viewModel)
        {
            var fet = db.Features.First(n => n.FeatureId == viewModel.FeatureId );
            var model = db.AssetTemplateFeatures.FirstOrDefault(n => n.FeatureId== fet.FeatureId && n.AssetTemplateId == viewModel.AssetTemplateId);
            if ((model == null) == viewModel.Assigned)
            {
                if (model == null)
                {
                    db.AssetTemplateFeatures.Add(new TAssetTemplateFeature()
                    {
                        Feature= fet,
                        AssetTemplateId = viewModel.AssetTemplateId
                    });
                }
                else
                {
                    db.AssetTemplateFeatures.Remove(model);
                }

                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadPaths([DataSourceRequest] DataSourceRequest request, [FromQuery] int assetTemplateId)
        {
            return Json(db.AssetTemplatePathFilters.Where(n => n.AssetTemplateId == assetTemplateId).ToDataSourceResult(request, n => n.ToViewModel<TAssetTemplatePath, AssetTemplatePathViewModel>()));
        }

        [HttpPost]
        public async Task<IActionResult> CreatePath([DataSourceRequest] DataSourceRequest request, [FromQuery] int assetTemplateId)
        {
            var model = new TAssetTemplatePath();
            if (ModelState.IsValid)
            {

                await this.TryUpdateModelAsync<AssetTemplatePathViewModel, TAssetTemplatePath>(model);
                model.AssetTemplateId = assetTemplateId;
                db.AssetTemplatePathFilters.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TAssetTemplatePath, AssetTemplatePathViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> DestroyPath([DataSourceRequest] DataSourceRequest request, AssetTemplatePathViewModel viewModel)
        {
            var model = db.AssetTemplatePathFilters.First(n => n.AssetTemplatePathId == viewModel.AssetTemplatePathId);
            if (ModelState.IsValid)
            {
                db.AssetTemplatePathFilters.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePath([DataSourceRequest] DataSourceRequest request, AssetTemplatePathViewModel viewModel)
        {
            var model = db.AssetTemplatePathFilters.First(n => n.AssetTemplatePathId == viewModel.AssetTemplatePathId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<AssetTemplatePathViewModel, TAssetTemplatePath>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TAssetTemplatePath, AssetTemplatePathViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
