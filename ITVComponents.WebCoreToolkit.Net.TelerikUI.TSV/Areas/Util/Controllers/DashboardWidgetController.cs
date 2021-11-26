using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(DashboardWidgets.View,DashboardWidgets.Write)"), Area("Util")]
    public class DashboardWidgetController : Controller
    {
        private readonly SecurityContext db;

        public DashboardWidgetController(SecurityContext db)
        {
            this.db = db;
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
            db.ShowAllTenants = true;
            return Json(db.Widgets.ToDataSourceResult(request,
                n => n.ToViewModel<DashboardWidget, DashboardWidgetViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,
            DashboardWidgetViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = new DashboardWidget();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardWidgetViewModel, DashboardWidget>(model);
                db.Widgets.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<DashboardWidget, DashboardWidgetViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            DashboardWidgetViewModel viewModel)
        {
            db.ShowAllTenants = true;
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
            db.ShowAllTenants = true;
            var model = db.Widgets.First(n => n.DashboardWidgetId == viewModel.DashboardWidgetId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardWidgetViewModel, DashboardWidget>(model, "",
                    m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<DashboardWidget, DashboardWidgetViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadParameters([DataSourceRequest] DataSourceRequest request, int dashboardWidgetId)
        {
            db.ShowAllTenants = true;
            return Json(db.WidgetParams.Where(n => n.DashboardWidgetId == dashboardWidgetId).ToDataSourceResult(request, n => n.ToViewModel<DashboardParam, DashboardParamViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> CreateParameter([DataSourceRequest] DataSourceRequest request, [FromQuery] int dashboardWidgetId)
        {
            db.ShowAllTenants = true;
            var model = new DashboardParam();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardParamViewModel, DashboardParam>(model);
                model.DashboardWidgetId = dashboardWidgetId;
                db.WidgetParams.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<DashboardParam, DashboardParamViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DashboardWidgets.Write)")]
        public async Task<IActionResult> DestroyParameter([DataSourceRequest] DataSourceRequest request, DashboardParamViewModel viewModel)
        {
            db.ShowAllTenants = true;
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
        public async Task<IActionResult> UpdateParameter([DataSourceRequest] DataSourceRequest request, DashboardParam viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.WidgetParams.First(n => n.DashboardParamId == viewModel.DashboardParamId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DashboardParamViewModel, DashboardParam>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<DashboardParam, DashboardParamViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}