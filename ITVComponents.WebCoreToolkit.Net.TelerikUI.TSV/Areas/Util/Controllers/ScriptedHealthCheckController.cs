using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(HealthChecks.View,HealthChecks.Write,Sysadmin),HasFeature(ITVAdminViews)"),Area("Util")]
    public class ScriptedHealthCheckController : Controller
    {
        private readonly ICoreSystemContext db;

        public ScriptedHealthCheckController(ICoreSystemContext db)
        {
            this.db = db;
            //db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ReadScripts([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.HealthScripts.ToDataSourceResult(request, n => n.ToViewModel<HealthScript, HealthScriptViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(HealthChecks.Write,Sysadmin)")]
        public async Task<IActionResult> CreateScript([DataSourceRequest] DataSourceRequest request)
        {
            var model = new HealthScript();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<HealthScriptViewModel,HealthScript>(model);
                db.HealthScripts.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<HealthScript, HealthScriptViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(HealthChecks.Write,Sysadmin)")]
        public async Task<IActionResult> UpdateScript([DataSourceRequest] DataSourceRequest request, HealthScriptViewModel viewModel)
        {
            var model = db.HealthScripts.First(n => n.HealthScriptId== viewModel.HealthScriptId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<HealthScriptViewModel,HealthScript>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<HealthScript, HealthScriptViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(HealthChecks.Write,Sysadmin)")]
        public async Task<IActionResult> DestroyScript([DataSourceRequest] DataSourceRequest request, HealthScriptViewModel viewModel)
        {
            var model = db.HealthScripts.First(n => n.HealthScriptId == viewModel.HealthScriptId);
            if (ModelState.IsValid)
            {
                db.HealthScripts.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
