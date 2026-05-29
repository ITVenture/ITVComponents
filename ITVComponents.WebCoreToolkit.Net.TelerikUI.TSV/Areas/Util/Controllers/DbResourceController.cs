using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Localization;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using DbLocale = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Localization;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(DbResources.View,DbResources.Write),HasFeature(ITVAdminViews)"), Area("Util")]
    public class DbResourceController:Controller
    {
        private readonly ICoreSystemContext basicContext;
        private readonly IStringLocalizerFactory localizerFactory;

        public DbResourceController(ICoreSystemContext basicContext, IStringLocalizerFactory localizerFactory)
        {
            this.basicContext = basicContext;
            this.localizerFactory = localizerFactory;
        }

        public IActionResult Index()
        {
            ViewData["CanResetStrings"] = localizerFactory is ContextLocalizerFactory;
            return View();
        }

        public IActionResult Cultures(int localizationId)
        {
            ViewData["localizationId"] = localizationId;
            return View();
        }

        public IActionResult Strings(int localizationCultureId)
        {
            ViewData["CanResetStrings"] = localizerFactory is ContextLocalizerFactory;
            ViewData["localizationCultureId"] = localizationCultureId;
            return View();
        }

        [HttpPost, Authorize("HasPermission(DbResources.Write)")]
        public IActionResult ResetLocalization()
        {
            if (localizerFactory is ContextLocalizerFactory cfac)
            {
                cfac.ResetLocalizers();
            }

            return Ok();
        }

        [HttpPost, Authorize("HasPermission(DbResources.Write)")]
        public IActionResult CopyFromSource(int localizationCultureId)
        {
            var record = basicContext.LocalizationCultures.First(n => n.LocalizationCultureId == localizationCultureId);
            var resourceIdentifier = record.Localization.Identifier;
            var tp = Type.GetType(resourceIdentifier, false);
            IStringLocalizer localizer;
            if (tp != null)
            {
                localizer = localizerFactory.Create(tp);
            }
            else
            {
                var id = resourceIdentifier.LastIndexOf(".");
                if (id != -1)
                {
                    localizer = localizerFactory.Create(resourceIdentifier.Substring(0, id),
                        resourceIdentifier.Substring(id + 1));
                }
                else
                {
                    localizer = null;
                }
            }

            if (localizer != null)
            {
                var strings = localizer.GetAllStrings(true);
                foreach (var s in strings)
                {
                    var rec = basicContext.LocalizationCultureStrings.FirstOrDefault(n =>
                        n.LocalizationCultureId == localizationCultureId && n.LocalizationKey == s.Name);
                    if (rec == null)
                    {
                        rec = new LocalizationString
                        {
                            LocalizationCultureId = localizationCultureId, LocalizationKey = s.Name,
                            LocalizationValue = s.Value
                        };

                        basicContext.LocalizationCultureStrings.Add(rec);
                    }
                }

                basicContext.SaveChanges();
            }

            return Ok();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(basicContext.Localizations.ToDataSourceResult(request,
                n => n.ToViewModel<DbLocale, LocalizationViewModel>()));
        }
        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,
            LocalizationViewModel viewModel)
        {
            var model = new DbLocale();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<LocalizationViewModel, DbLocale>(model);
                basicContext.Localizations.Add(model);
                await basicContext.SaveChangesAsync();
            }
            return Json(await new[] { model.ToViewModel<DbLocale, LocalizationViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request,
            LocalizationViewModel viewModel)
        {
            var model = basicContext.Localizations.First(n => n.LocalizationId == viewModel.LocalizationId);
            if (ModelState.IsValid)
            {
                basicContext.Localizations.Remove(model);
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request,
            LocalizationViewModel viewModel)
        {
            var model = basicContext.Localizations.First(n => n.LocalizationId== viewModel.LocalizationId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<LocalizationViewModel, DbLocale>(model, "",
                    m => { return m.ElementType == null; });
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<DbLocale, LocalizationViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadCultures([DataSourceRequest] DataSourceRequest request, int localizationId)
        {
            return Json(basicContext.LocalizationCultures.Where(n => n.LocalizationId == localizationId).ToDataSourceResult(request,
                n => n.ToViewModel<LocalizationCulture, LocalizationCultureViewModel>()));
        }
        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> CreateCulture([DataSourceRequest] DataSourceRequest request,
            LocalizationCultureViewModel viewModel, int localizationId)
        {
            var model = new LocalizationCulture();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<LocalizationCultureViewModel, LocalizationCulture>(model);
                model.LocalizationId = localizationId;
                basicContext.LocalizationCultures.Add(model);
                await basicContext.SaveChangesAsync();
            }
            return Json(await new[] { model.ToViewModel<LocalizationCulture, LocalizationCultureViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> DestroyCulture([DataSourceRequest] DataSourceRequest request,
            LocalizationCultureViewModel viewModel)
        {
            var model = basicContext.LocalizationCultures.First(n => n.LocalizationCultureId == viewModel.LocalizationCultureId);
            if (ModelState.IsValid)
            {
                basicContext.LocalizationCultures.Remove(model);
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> UpdateCulture([DataSourceRequest] DataSourceRequest request,
            LocalizationCultureViewModel viewModel)
        {
            var model = basicContext.LocalizationCultures.First(n => n.LocalizationCultureId == viewModel.LocalizationCultureId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<LocalizationCultureViewModel, LocalizationCulture>(model, "",
                    m => { return m.ElementType == null; });
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<LocalizationCulture, LocalizationCultureViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadStrings([DataSourceRequest] DataSourceRequest request, int localizationCultureId)
        {
            return Json(basicContext.LocalizationCultureStrings.Where(n => n.LocalizationCultureId== localizationCultureId).ToDataSourceResult(request,
                n => n.ToViewModel<LocalizationString, LocalizationStringViewModel>()));
        }
        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> CreateString([DataSourceRequest] DataSourceRequest request,
            LocalizationStringViewModel viewModel, int localizationCultureId)
        {
            var model = new LocalizationString();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<LocalizationStringViewModel, LocalizationString>(model);
                model.LocalizationCultureId= localizationCultureId;
                basicContext.LocalizationCultureStrings.Add(model);
                await basicContext.SaveChangesAsync();
            }
            return Json(await new[] { model.ToViewModel<LocalizationString, LocalizationStringViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> DestroyString([DataSourceRequest] DataSourceRequest request,
            LocalizationStringViewModel viewModel)
        {
            var model = basicContext.LocalizationCultureStrings.First(n => n.LocalizationStringId== viewModel.LocalizationStringId);
            if (ModelState.IsValid)
            {
                basicContext.LocalizationCultureStrings.Remove(model);
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DbResources.Write)")]
        public async Task<IActionResult> UpdateString([DataSourceRequest] DataSourceRequest request,
            LocalizationStringViewModel viewModel)
        {
            var model = basicContext.LocalizationCultureStrings.First(n => n.LocalizationStringId== viewModel.LocalizationStringId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<LocalizationStringViewModel, LocalizationString>(model, "",
                    m => { return m.ElementType == null; });
                await basicContext.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<LocalizationString, LocalizationStringViewModel>() }
                .ToDataSourceResultAsync(request, ModelState));
        }
    }
}
