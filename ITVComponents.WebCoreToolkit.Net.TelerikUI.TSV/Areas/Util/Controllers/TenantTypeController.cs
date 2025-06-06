using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(Sysadmin),HasFeature(ITVAdminViews)"), Area("Util")]
    public class TenantTypeController:Controller
    {
        private readonly ICoreSystemContext db;

        public TenantTypeController(ICoreSystemContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.TenantTypes.ToDataSourceResult(request, ModelState, n => n.ToViewModel<TenantType, TenantTypeViewModel>()));
        }

        [HttpPost]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new TenantType();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantTypeViewModel, TenantType>(model);
                db.TenantTypes.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TenantType, TenantTypeViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, TenantTypeViewModel viewModel)
        {

            var model = db.TenantTypes.First(n => n.TenantTypeId == viewModel.TenantTypeId);
            if (ModelState.IsValid)
            {
                db.TenantTypes.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, TenantTypeViewModel viewModel)
        {
            var model = db.TenantTypes.First(n => n.TenantTypeId== viewModel.TenantTypeId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantTypeViewModel, TenantType>(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<TenantType, TenantTypeViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        public async Task<IActionResult> ApplyTemplate([FromBody]ApplyTenantTemplateViewModel tvm)
        {
            var tenantType = db.TenantTypes.First(n => n.TenantTypeId == tvm.TenantTypeId);
            HttpContext.RequestServices.ApplyTenantTemplates(db, tenantType);
            return Json("ok");
        }
    }
}
