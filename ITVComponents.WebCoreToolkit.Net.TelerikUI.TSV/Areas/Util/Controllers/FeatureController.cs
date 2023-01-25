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
            return PartialView(tenantId);
        }

        public IActionResult ModuleTable(int featureId)
        {
            return PartialView(featureId);
        }

        public IActionResult ModuleDetails(int featureId, int templateModuleId)
        {
            ViewData["featureId"] = featureId;
            ViewData["templateModuleId"] = templateModuleId;
            return PartialView();
        }

        public IActionResult ModuleConfiguratorTable(int featureId, int templateModuleId)
        {
            var tmp = db.TemplateModules.FirstOrDefault(n =>
                n.FeatureId == featureId && n.TemplateModuleId == templateModuleId);
            if (tmp != null)
            {
                return PartialView(templateModuleId);
            }

            return NotFound();
        }

        public IActionResult ModuleScriptTable(int featureId, int templateModuleId)
        {
            var tmp = db.TemplateModules.FirstOrDefault(n =>
                n.FeatureId == featureId && n.TemplateModuleId == templateModuleId);
            if (tmp != null)
            {
                return PartialView(templateModuleId);
            }

            return NotFound();
        }

        public IActionResult ModuleConfigParamTable(int templateModuleId, int templateModuleConfiguratorId)
        {
            var tmp = db.TemplateModuleConfigurators.FirstOrDefault(n =>
                n.TemplateModuleId== templateModuleId&& n.TemplateModuleConfiguratorId== templateModuleConfiguratorId);
            if (tmp != null)
            {
                return PartialView(templateModuleConfiguratorId);
            }

            return NotFound();
        }

        public IActionResult ActivationTable(int featureId, int tenantId)
        {
            ViewData["featureId"] = featureId;
            ViewData["tenantId"] = tenantId;
            return PartialView();
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

        //activations
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

        //templateModules
        [HttpPost]
        public IActionResult ReadTemplateModules([DataSourceRequest] DataSourceRequest request, [FromQuery] int featureId)
        {
            return Json(db.TemplateModules.Where(n => n.FeatureId == featureId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TemplateModule, TemplateModuleViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> CreateTemplateModule([DataSourceRequest] DataSourceRequest request, [FromQuery] int featureId)
        {
            var model = new TemplateModule();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleViewModel, TemplateModule>(model);
                model.FeatureId = featureId;
                db.TemplateModules.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModule, TemplateModuleViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> DestroyTemplateModule([DataSourceRequest] DataSourceRequest request, TemplateModuleViewModel viewModel)
        {

            var model = db.TemplateModules.First(n => n.TemplateModuleId == viewModel.TemplateModuleId);
            if (ModelState.IsValid)
            {
                db.TemplateModules.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> UpdateTemplateModule([DataSourceRequest] DataSourceRequest request, TemplateModuleViewModel viewModel)
        {
            var model = db.TemplateModules.First(n => n.TemplateModuleId == viewModel.TemplateModuleId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleViewModel, TemplateModule>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModule, TemplateModuleViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        //templateModuleConfigurators
        [HttpPost]
        public IActionResult ReadConfigurators([DataSourceRequest] DataSourceRequest request, [FromQuery] int templateModuleId)
        {
            return Json(db.TemplateModuleConfigurators.Where(n => n.TemplateModuleId == templateModuleId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TemplateModuleConfigurator, TemplateModuleConfiguratorViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> CreateConfigurator([DataSourceRequest] DataSourceRequest request, [FromQuery] int templateModuleId)
        {
            var model = new TemplateModuleConfigurator();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleConfiguratorViewModel, TemplateModuleConfigurator>(model);
                model.TemplateModuleId = templateModuleId;
                db.TemplateModuleConfigurators.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModuleConfigurator, TemplateModuleConfiguratorViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> DestroyConfigurator([DataSourceRequest] DataSourceRequest request, TemplateModuleConfiguratorViewModel viewModel)
        {

            var model = db.TemplateModuleConfigurators.First(n => n.TemplateModuleConfiguratorId== viewModel.TemplateModuleConfiguratorId);
            if (ModelState.IsValid)
            {
                db.TemplateModuleConfigurators.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> UpdateConfigurator([DataSourceRequest] DataSourceRequest request, TemplateModuleConfiguratorViewModel viewModel)
        {
            var model = db.TemplateModuleConfigurators.First(n => n.TemplateModuleConfiguratorId== viewModel.TemplateModuleConfiguratorId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleConfiguratorViewModel, TemplateModuleConfigurator>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModuleConfigurator, TemplateModuleConfiguratorViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        //TemplateModuleScripts
        [HttpPost]
        public IActionResult ReadScripts([DataSourceRequest] DataSourceRequest request, [FromQuery] int templateModuleId)
        {
            return Json(db.TemplateModuleScripts.Where(n => n.TemplateModuleId == templateModuleId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TemplateModuleScript, TemplateModuleScriptViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> CreateScript([DataSourceRequest] DataSourceRequest request, [FromQuery] int templateModuleId)
        {
            var model = new TemplateModuleScript();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleScriptViewModel, TemplateModuleScript>(model);
                model.TemplateModuleId = templateModuleId;
                db.TemplateModuleScripts.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModuleScript, TemplateModuleScriptViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> DestroyScript([DataSourceRequest] DataSourceRequest request, TemplateModuleScriptViewModel viewModel)
        {

            var model = db.TemplateModuleScripts.First(n => n.TemplateModuleScriptId== viewModel.TemplateModuleScriptId);
            if (ModelState.IsValid)
            {
                db.TemplateModuleScripts.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> UpdateScript([DataSourceRequest] DataSourceRequest request, TemplateModuleScriptViewModel viewModel)
        {
            var model = db.TemplateModuleScripts.First(n => n.TemplateModuleScriptId == viewModel.TemplateModuleScriptId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleScriptViewModel, TemplateModuleScript>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModuleScript, TemplateModuleScriptViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        //templateModuleConfigurators
        [HttpPost]
        public IActionResult ReadConfiguratorParams([DataSourceRequest] DataSourceRequest request, [FromQuery] int templateModuleConfiguratorId)
        {
            return Json(db.TemplateModuleConfiguratorParameters.Where(n => n.TemplateModuleConfiguratorId == templateModuleConfiguratorId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TemplateModuleConfiguratorParameter, TemplateModuleConfiguratorParameterViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> CreateConfiguratorParam([DataSourceRequest] DataSourceRequest request, [FromQuery] int templateModuleConfiguratorId)
        {
            var model = new TemplateModuleConfiguratorParameter();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleConfiguratorParameterViewModel, TemplateModuleConfiguratorParameter>(model);
                model.TemplateModuleConfiguratorId = templateModuleConfiguratorId;
                db.TemplateModuleConfiguratorParameters.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModuleConfiguratorParameter, TemplateModuleConfiguratorParameterViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> DestroyConfiguratorParam([DataSourceRequest] DataSourceRequest request, TemplateModuleConfiguratorParameterViewModel viewModel)
        {

            var model = db.TemplateModuleConfiguratorParameters.First(n => n.TemplateModuleCfgParameterId == viewModel.TemplateModuleCfgParameterId);
            if (ModelState.IsValid)
            {
                db.TemplateModuleConfiguratorParameters.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Sysadmin)")]
        public async Task<IActionResult> UpdateConfiguratorParam([DataSourceRequest] DataSourceRequest request, TemplateModuleConfiguratorParameterViewModel viewModel)
        {
            var model = db.TemplateModuleConfiguratorParameters.First(n => n.TemplateModuleCfgParameterId == viewModel.TemplateModuleCfgParameterId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TemplateModuleConfiguratorParameterViewModel, TemplateModuleConfiguratorParameter>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TemplateModuleConfiguratorParameter, TemplateModuleConfiguratorParameterViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
