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
    [Authorize("HasPermission(GlobalSettings.View,GlobalSettings.Write),HasFeature(ITVAdminViews)"),Area("Util")]
    public class GlobalSettingsController:Controller
    {
        private readonly IBaseTenantContext db;

        public GlobalSettingsController(IBaseTenantContext db)
        {
            this.db = db;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ReadSettings([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.GlobalSettings.ToDataSourceResult(request, n => n.ToViewModel<GlobalSetting, GlobalSettingViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalSettings.Write)")]
        public async Task<IActionResult> CreateSetting([DataSourceRequest] DataSourceRequest request)
        {
            var model = new GlobalSetting();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<GlobalSettingViewModel,GlobalSetting>(model);
                if (model.JsonSetting)
                {
                    model.SettingsValue = model.SettingsValue.EncryptJsonValues();
                }
                db.GlobalSettings.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<GlobalSetting, GlobalSettingViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalSettings.Write)")]
        public async Task<IActionResult> UpdateSetting([DataSourceRequest] DataSourceRequest request, GlobalSettingViewModel viewModel)
        {
            var model = db.GlobalSettings.First(n => n.GlobalSettingId == viewModel.GlobalSettingId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<GlobalSettingViewModel,GlobalSetting>(model, "", m => { return m.ElementType == null; });
                if (model.JsonSetting)
                {
                    model.SettingsValue = model.SettingsValue.EncryptJsonValues();
                }
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<GlobalSetting, GlobalSettingViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(GlobalSettings.Write)")]
        public async Task<IActionResult> DestroySetting([DataSourceRequest] DataSourceRequest request, GlobalSettingViewModel viewModel)
        {
            var model = db.GlobalSettings.First(n => n.GlobalSettingId == viewModel.GlobalSettingId);
            if (ModelState.IsValid)
            {
                db.GlobalSettings.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
