using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(DashboardWidgets.View,DashboardWidgets.Write),HasFeature(ITVAdminViews)"), Area("Util"), ConstructedGenericControllerConvention]
    public class DashboardWidgetController<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TContext> : Controller
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>, new()
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>, new()
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    {
        private readonly TContext db;

        public DashboardWidgetController(TContext db)
        {
            this.db = db;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Param(int dashboardWidgetId)
        {
            ViewData["ParameterTypes"] = new SelectList(EnumHelper.DescribeEnum<InputType>(), "Value", "Description");
            return View(dashboardWidgetId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.Widgets.ToDataSourceResult(request,
                n => n.ToViewModel<TWidget, DashboardWidgetViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,
            DashboardWidgetViewModel viewModel)
        {
            var model = new TWidget();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardWidgetViewModel, TWidget>(model);
                db.Widgets.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TWidget, DashboardWidgetViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            DashboardWidgetViewModel viewModel)
        {
            var model = db.Widgets.First(n => n.DashboardWidgetId == viewModel.DashboardWidgetId);
            if (ModelState.IsValid)
            {
                db.Widgets.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            DashboardWidgetViewModel viewModel)
        {
            var model = db.Widgets.First(n => n.DashboardWidgetId == viewModel.DashboardWidgetId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardWidgetViewModel, TWidget>(model, "",
                    m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TWidget, DashboardWidgetViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadParameters([DataSourceRequest] DataSourceRequest request, int dashboardWidgetId)
        {
            return Json(db.WidgetParams.Where(n => n.DashboardWidgetId == dashboardWidgetId).ToDataSourceResult(request, n => n.ToViewModel<TWidgetParam, DashboardParamViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> CreateParameter([DataSourceRequest] DataSourceRequest request, [FromQuery] int dashboardWidgetId)
        {
            var model = new TWidgetParam();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardParamViewModel, TWidgetParam>(model);
                model.DashboardWidgetId = dashboardWidgetId;
                db.WidgetParams.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TWidgetParam, DashboardParamViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> DestroyParameter([DataSourceRequest] DataSourceRequest request, DashboardParamViewModel viewModel)
        {
            var model = db.WidgetParams.First(n => n.DashboardParamId == viewModel.DashboardParamId);
            if (ModelState.IsValid)
            {
                db.WidgetParams.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> UpdateParameter([DataSourceRequest] DataSourceRequest request, DashboardParamViewModel viewModel)
        {
            var model = db.WidgetParams.First(n => n.DashboardParamId == viewModel.DashboardParamId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardParamViewModel, TWidgetParam>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TWidgetParam, DashboardParamViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}