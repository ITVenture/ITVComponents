using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TextsAndMessagesHelper = ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Resources.TextsAndMessagesHelper;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Sysadmin),HasFeature(ITVAdminViews)"), Area("Security")]
    public class TrustedComponentController:Controller
    {
       private readonly ICoreSystemContext db;
       private bool isSysAdmin;

        public TrustedComponentController(ICoreSystemContext db, IServiceProvider services)
        {
            this.db = db;
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
            return View();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.TrustedFullAccessComponents.ToDataSourceResult(request,
                n => n.ToViewModel<TrustedFullAccessComponent, TrustedFullAccessComponentViewModel>()));
        }

        [HttpPost]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new TrustedFullAccessComponent();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TrustedFullAccessComponentViewModel,TrustedFullAccessComponent>(model);
                db.TrustedFullAccessComponents.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TrustedFullAccessComponent, TrustedFullAccessComponentViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, TrustedFullAccessComponentViewModel viewModel)
        {
            var model = db.TrustedFullAccessComponents.First(n => n.TrustedFullAccessComponentId== viewModel.TrustedFullAccessComponentId);
            if (ModelState.IsValid)
            {
                db.TrustedFullAccessComponents.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, TrustedFullAccessComponentViewModel viewModel)
        {
            var model = db.TrustedFullAccessComponents.First(n => n.TrustedFullAccessComponentId == viewModel.TrustedFullAccessComponentId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TrustedFullAccessComponentViewModel, TrustedFullAccessComponent>(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
