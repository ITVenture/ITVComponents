using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Areas.Util.Controllers
{
    [Authorize("HasPermission(PlugIns.Write,PlugIns.View)"), Area("Util")]
    public class PlugInController : Controller
    {
        private readonly SecurityContext db;
        private readonly IInjectablePlugin<WebPluginAnalyzer> analyzer;
        private readonly bool isSysAdmin;

        public PlugInController(SecurityContext db, IInjectablePlugin<WebPluginAnalyzer> analyzer, IServiceProvider services)
        {
            this.db = db;
            this.analyzer = analyzer;
            if (!services.VerifyUserPermissions(new[] {ToolkitPermission.Sysadmin}))
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

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }

            return Json(db.WebPlugins.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, ModelState, n => n.ToViewModel<WebPlugin, WebPluginViewModel>()));
        }

        public IActionResult AnalyzeAssembly(string assemblyName)
        {
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
            
            var model = new WebPlugin();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<WebPluginViewModel,WebPlugin>(model);
                model.TenantId = tenantId;
                db.WebPlugins.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<WebPlugin, WebPluginViewModel>()}.ToDataSourceResultAsync(request, ModelState));
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
                await this.TryUpdateModelAsync<WebPluginViewModel,WebPlugin>(model, "", m => { return m.ElementType == null; });
                model.TenantId = tenantId;
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<WebPlugin, WebPluginViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
