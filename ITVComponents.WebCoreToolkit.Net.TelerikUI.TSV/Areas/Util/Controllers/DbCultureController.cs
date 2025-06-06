using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
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
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(DbCultures.View,DbCultures.Write),HasFeature(ITVAdminViews)"), Area("Util")]
    public class DbCultureController:Controller
    {
        private readonly ICoreSystemContext basicContext;

        public DbCultureController(ICoreSystemContext basicContext)
        {
            this.basicContext = basicContext;
        }

        public IActionResult Index()
        {
            return View();
        }


        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(basicContext.Cultures.ToDataSourceResult(request,
                n => n.ToViewModel<Culture, CultureViewModel>()));
        }
        [HttpPost]
        [Authorize("HasPermission(DbCultures.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,
            CultureViewModel viewModel)
        {
            var model = new Culture();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<CultureViewModel, Culture>(model);
                basicContext.Cultures.Add(model);
                await basicContext.SaveChangesAsync();
            }
            return Json(await new[] { model.ToViewModel<Culture, CultureViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbCultures.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            CultureViewModel viewModel)
        {
            var model = basicContext.Cultures.First(n => n.CultureId== viewModel.CultureId);
            if (ModelState.IsValid)
            {
                basicContext.Cultures.Remove(model);
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbCultures.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            CultureViewModel viewModel)
        {
            var model = basicContext.Cultures.First(n => n.CultureId== viewModel.CultureId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<CultureViewModel, Culture>(model, "",
                    m => { return m.ElementType == null; });
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<Culture, CultureViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }
    }
}
