using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(PlugIns.Write,PlugIns.View),HasFeature(ITVAdminViews)"), Area("Util"), ConstructedGenericControllerConvention]
    public class PlugInController<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> : Controller 
        where TTenant : Tenant 
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>, new()
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
    {
        private readonly IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db;
        private readonly IInjectablePlugin<WebPluginAnalyzer> analyzer;
        private readonly bool isSysAdmin;

        public PlugInController(IBaseTenantContext<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation> db, IInjectablePlugin<WebPluginAnalyzer> analyzer, IServiceProvider services)
        {
            this.db = db;
            this.analyzer = analyzer;
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
            int? tenantId = null;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }

            ViewData["tenantId"] = tenantId;
            return View();
        }

        public IActionResult PluginTable(int tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
            }

            return View(tenantId);
        }

        public IActionResult ArgumentsTable(int pluginId)
        {
            return View(pluginId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }

            return Json(db.WebPlugins.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TWebPlugin, WebPluginViewModel>()));
        }

        public IActionResult AnalyzeAssembly(string assemblyName)
        {
            db.ShowAllTenants = false;
            db.HideGlobals = false;
            return PartialView("_AnalyzeAssembly", assemblyName);
        }

        [HttpPost]
        [Authorize("HasPermission(PlugIns.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,[FromQuery] int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            var model = new TWebPlugin();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginViewModel,TWebPlugin>(model);
                model.TenantId = tenantId;
                db.WebPlugins.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TWebPlugin, WebPluginViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugIns.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, WebPluginViewModel viewModel)
        {

            var tenantId = viewModel.TenantId;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            var model = db.WebPlugins.First(n => n.WebPluginId == viewModel.WebPluginId && n.TenantId == tenantId);
            if (ModelState.IsValid)
            {
                db.WebPlugins.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugIns.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, WebPluginViewModel viewModel)
        {
            var tenantId = viewModel.TenantId;
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            var model = db.WebPlugins.First(n => n.WebPluginId == viewModel.WebPluginId && n.TenantId == tenantId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginViewModel, TWebPlugin>(model, "", m => { return m.ElementType == null; });
                model.TenantId = tenantId;
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TWebPlugin, WebPluginViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        public IActionResult ReadArgs([DataSourceRequest] DataSourceRequest request, [FromQuery] int pluginId)
        {
            return Json(db.GenericPluginParams.Where(n => n.WebPluginId== pluginId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<TWebPluginGenericParameter, WebPluginGenericParameterViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugIns.Write)")]
        public async Task<IActionResult> CreateArg([DataSourceRequest] DataSourceRequest request, [FromQuery] int pluginId)
        {
            var model = new TWebPluginGenericParameter();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginGenericParameterViewModel, TWebPluginGenericParameter>(model);
                model.WebPluginId = pluginId;
                db.GenericPluginParams.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TWebPluginGenericParameter, WebPluginGenericParameterViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugIns.Write)")]
        public async Task<IActionResult> DestroyArg([DataSourceRequest] DataSourceRequest request, WebPluginGenericParameterViewModel viewModel)
        {
            var model = db.GenericPluginParams.First(n => n.WebPluginGenericParameterId == viewModel.WebPluginGenericParameterId);
            if (ModelState.IsValid)
            {
                db.GenericPluginParams.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(PlugIns.Write)")]
        public async Task<IActionResult> UpdateArg([DataSourceRequest] DataSourceRequest request, WebPluginGenericParameterViewModel viewModel)
        {
            var model = db.GenericPluginParams.First(n => n.WebPluginGenericParameterId == viewModel.WebPluginGenericParameterId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginGenericParameterViewModel, TWebPluginGenericParameter>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TWebPluginGenericParameter, WebPluginGenericParameterViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
