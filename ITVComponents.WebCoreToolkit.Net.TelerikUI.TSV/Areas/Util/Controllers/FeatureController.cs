using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(Sysadmin),HasFeature(ITVAdminViews)"), Area("Util")]
    public class FeatureController : Controller
    {
        private readonly IBaseTenantContext db;

        public FeatureController(IBaseTenantContext db)
        {
            this.db = db;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult FeatureTable(int tenantId)
        {
            return View(tenantId);
        }

        public IActionResult ActivationTable(int featureId, int tenantId)
        {
            ViewData["featureId"] = featureId;
            ViewData["tenantId"] = tenantId;
            return View();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, [FromQuery]int? tenantId)
        {
            return Json(db.Features.ToDataSourceResult(request, ModelState, n => n.ToViewModel<Feature, FeatureViewModel>((m,v) => v.TenantId = tenantId)));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new Feature();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<FeatureViewModel, Feature>(model);
                db.Features.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<Feature, FeatureViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, FeatureViewModel viewModel)
        {

            var model = db.Features.First(n => n.FeatureId== viewModel.FeatureId);
            if (ModelState.IsValid)
            {
                db.Features.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, FeatureViewModel viewModel)
        {
            var model = db.Features.First(n => n.FeatureId == viewModel.FeatureId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<FeatureViewModel, Feature>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<Feature, FeatureViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        //holdrio
        [HttpPost]
        public IActionResult ReadActivations([DataSourceRequest] DataSourceRequest request, [FromQuery] int tenantId, [FromQuery]int featureId)
        {
            return Json(db.TenantFeatureActivations.Where(n => n.TenantId == tenantId && n.FeatureId == featureId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TenantFeatureActivation, FeatureActivationViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> CreateActivation([DataSourceRequest] DataSourceRequest request, [FromQuery] int tenantId, [FromQuery] int featureId)
        {
            var model = new TenantFeatureActivation();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<FeatureActivationViewModel, TenantFeatureActivation>(model);
                model.TenantId = tenantId;
                model.FeatureId = featureId;
                db.TenantFeatureActivations.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TenantFeatureActivation, FeatureActivationViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> DestroyActivation([DataSourceRequest] DataSourceRequest request, FeatureActivationViewModel viewModel)
        {

            var model = db.TenantFeatureActivations.First(n => n.TenantFeatureActivationId == viewModel.TenantFeatureActivationId);
            if (ModelState.IsValid)
            {
                db.TenantFeatureActivations.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> UpdateActivation([DataSourceRequest] DataSourceRequest request, FeatureActivationViewModel viewModel)
        {
            var model = db.TenantFeatureActivations.First(n => n.TenantFeatureActivationId == viewModel.TenantFeatureActivationId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<FeatureActivationViewModel, TenantFeatureActivation>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TenantFeatureActivation, FeatureActivationViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
