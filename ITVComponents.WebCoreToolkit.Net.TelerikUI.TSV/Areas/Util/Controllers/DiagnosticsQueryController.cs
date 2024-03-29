﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using ITVComponents.WebCoreToolkit.Security;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(DiagnosticsQueries.View,DiagnosticsQueries.Write),HasFeature(ITVAdminViews)"), Area("Util"), ConstructedGenericControllerConvention]
    public class DiagnosticsQueryController<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TContext> : Controller
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>, new()
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>, new()
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>, new()
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
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
        private readonly IOptions<SecurityViewsOptions> options;
        private readonly IPermissionScope permissionScope;

        public DiagnosticsQueryController(TContext db, IOptions<SecurityViewsOptions> options, IPermissionScope permissionScope)
        {
            this.db = db;
            this.options = options;
            this.permissionScope = permissionScope;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ParameterTable(int diagnosticsQueryId)
        {
            ViewData["diagnosticsQueryId"] = diagnosticsQueryId;
            ViewData["ParameterTypes"] = new SelectList(EnumHelper.DescribeEnum<QueryParameterTypes>(), "Value", "Description");
            return PartialView();
        }

        public IActionResult QueryDetailTabs(int diagnosticsQueryId)
        {
            ViewData["diagnosticsQueryId"] = diagnosticsQueryId;
            return PartialView();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.DiagnosticsQueries.ToDataSourceResult(request, n => n.ToViewModel<TQuery, DiagnosticsQueryViewModel>(ApplyTenants)));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryViewModel viewModel)
        {
            var model = new TQuery();
            if (ModelState.IsValid)
            {

/* Nicht gemergte Änderung aus Projekt "ITVComponents.WebCoreToolkit.Net.TelerikUi (netcoreapp3.1)"
Vor:
                await this.TryUpdateModelAsync<DiagnosticsQueryViewModel,DiagnosticsQuery>(model);
Nach:
                await this.TryUpdateModelAsync<DiagnosticsQueryViewModel, DiagnosticsQueryDefinition>(model);
*/
                await this.TryUpdateModelAsync<DiagnosticsQueryViewModel, TQuery>(model);
                db.DiagnosticsQueries.Add(model);
                SetTenants(viewModel.Tenants, model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TQuery, DiagnosticsQueryViewModel>(ApplyTenants)}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryViewModel viewModel)
        {
            var model = db.DiagnosticsQueries.First(n => n.DiagnosticsQueryId== viewModel.DiagnosticsQueryId);
            if (ModelState.IsValid)
            {
                db.TenantDiagnosticsQueries.RemoveRange(model.Tenants);
                db.DiagnosticsQueryParameters.RemoveRange(model.Parameters);
                db.DiagnosticsQueries.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryViewModel viewModel)
        {
            var model = db.DiagnosticsQueries.First(n => n.DiagnosticsQueryId == viewModel.DiagnosticsQueryId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryViewModel, TQuery>(model, "", m => { return m.ElementType == null; });
                SetTenants(viewModel.Tenants, model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TQuery, DiagnosticsQueryViewModel>(ApplyTenants)}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadParameters([DataSourceRequest] DataSourceRequest request, int diagnosticsQueryId)
        {
            return Json(db.DiagnosticsQueryParameters.Where(n => n.DiagnosticsQueryId == diagnosticsQueryId).ToDataSourceResult(request, n => n.ToViewModel<TQueryParameter, DiagnosticsQueryParameterViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> CreateParameter([DataSourceRequest] DataSourceRequest request, [FromQuery]int diagnosticsQueryId)
        {
            var model = new TQueryParameter();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryParameterViewModel, TQueryParameter>(model);
                model.DiagnosticsQueryId = diagnosticsQueryId;
                db.DiagnosticsQueryParameters.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TQueryParameter, DiagnosticsQueryParameterViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> DestroyParameter([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryParameterViewModel viewModel)
        {
            var model = db.DiagnosticsQueryParameters.First(n => n.DiagnosticsQueryParameterId == viewModel.DiagnosticsQueryParameterId);
            if (ModelState.IsValid)
            {
                db.DiagnosticsQueryParameters.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> UpdateParameter([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryParameterViewModel viewModel)
        {
            var model = db.DiagnosticsQueryParameters.First(n => n.DiagnosticsQueryParameterId == viewModel.DiagnosticsQueryParameterId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryParameterViewModel, TQueryParameter>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TQueryParameter, DiagnosticsQueryParameterViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        private void ApplyTenants(TQuery original, DiagnosticsQueryViewModel vm)
        {
            if (options.Value.TenantLinkMode == LinkMode.MultiSelect)
            {
                vm.Tenants = (from t in original.Tenants select t.TenantId).ToArray();
            }
            else
            {
                vm.Tenants = Array.Empty<int>();
            }
        }

        private void SetTenants(int[] tenants, TQuery query)
        {
            if (options.Value.TenantLinkMode == LinkMode.MultiSelect)
            {
                var tmp = (from t in query.Tenants
                    join nt in tenants on t.TenantId equals nt
                        into j
                    from o in j.DefaultIfEmpty()
                    where o == 0
                    select t).ToArray();
                var tmp2 = (from t in tenants
                    join ot in query.Tenants on t equals ot.TenantId
                        into j
                    from n in j.DefaultIfEmpty()
                    where n == null
                    select t).ToArray();
                db.TenantDiagnosticsQueries.RemoveRange(tmp);
                db.TenantDiagnosticsQueries.AddRange(from t in tmp2
                    select new TTenantQuery() { TenantId = t, DiagnosticsQuery = query });
            }
        }
    }
}
