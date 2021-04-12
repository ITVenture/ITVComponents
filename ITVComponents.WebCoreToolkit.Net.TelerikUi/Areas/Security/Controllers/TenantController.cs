using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Areas.Security.Controllers
{
    [Authorize("HasPermission(Tenants.View,Tenants.Write,Tenants.AssignUser,Tenants.WriteSettings)"), Area("Security")]
    public class TenantController : Controller
    {
        private readonly SecurityContext db;
        
        public TenantController(SecurityContext db, IServiceProvider services)
        {
            this.db = db;
            if (!services.VerifyUserPermissions(new[] {ToolkitPermission.Sysadmin}))
            {
                db.HideGlobals = true;
                //isSysAdmin = false;
            }
            else
            {
                db.ShowAllTenants = true;
                //isSysAdmin = true;
            }
        }

        [Authorize("HasPermission(Tenants.View,Tenants.Write)")]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize("HasPermission(Tenants.AssignUser)")]
        public IActionResult TenantTable(int userId)
        {
            return View(userId);
        }

        public IActionResult SettingsTable(int tenantId)
        {
            ViewData["tenantId"] = tenantId;
            return PartialView();
        }

        public IActionResult DetailsView(int tenantId)
        {
            return PartialView(tenantId);
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] int? userId)
        {
            if (userId == null)
            {
                return Json(db.Tenants.ToDataSourceResult(request, n => n.ToViewModel<Tenant, TenantViewModel>()));
            }

            return Json((from p in db.Tenants
                join r in db.TenantUsers on new {p.TenantId, UserId = userId.Value} equals new {r.TenantId, r.UserId} into lj
                from s in lj.DefaultIfEmpty()
                select new TenantViewModel
                {
                    TenantId = p.TenantId,
                    UserId = userId,
                    Assigned = s != null,
                    UniQUID = $"{p.TenantId}_{userId}",
                    DisplayName = p.DisplayName,
                    TenantName = p.TenantName
                }).ToDataSourceResult(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new Tenant();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantViewModel,Tenant>(model);
                db.Tenants.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<Tenant, TenantViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.Tenants.First(n => n.TenantId== viewModel.TenantId);
            if (ModelState.IsValid)
            {
                db.Tenants.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.Write,Tenants.AssignUser)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, TenantViewModel viewModel)
        {
            if (viewModel.UserId == null && HttpContext.RequestServices.VerifyUserPermissions(new[] {"Tenants.Write"}))
            {
                var model = db.Tenants.First(n => n.TenantId == viewModel.TenantId);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<TenantViewModel, Tenant>(model, "", m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<Tenant, TenantViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.UserId != null && HttpContext.RequestServices.VerifyUserPermissions(new[] {"Tenants.AssignUser"}))
            {
                var user = db.Users.First(n => n.UserId== viewModel.UserId);
                var model = db.TenantUsers.FirstOrDefault(n => n.UserId == viewModel.UserId && n.TenantId == viewModel.TenantId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.TenantUsers.Add(new TenantUser
                        {
                            TenantId = viewModel.TenantId,
                            UserId = user.UserId
                        });
                    }
                    else
                    {
                        db.TenantUsers.Remove(model);
                    }

                    await db.SaveChangesAsync();
                }

                return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        public IActionResult ReadSettings([DataSourceRequest] DataSourceRequest request, [FromQuery]int tenantId)
        {
            db.ShowAllTenants = true;
            return Json(db.TenantSettings.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, n => n.ToViewModel<TenantSetting, TenantSettingViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.WriteSettings)")]
        public async Task<IActionResult> CreateSetting([DataSourceRequest] DataSourceRequest request,[FromQuery] int tenantId)
        {
            var model = new TenantSetting();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantSettingViewModel,TenantSetting>(model);
                model.TenantId = tenantId;
                db.TenantSettings.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TenantSetting, TenantSettingViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.WriteSettings)")]
        public async Task<IActionResult> UpdateSetting([DataSourceRequest] DataSourceRequest request, TenantSettingViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.TenantSettings.First(n => n.TenantSettingId == viewModel.TenantSettingId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<TenantSettingViewModel,TenantSetting>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<TenantSetting, TenantSettingViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Tenants.WriteSettings)")]
        public async Task<IActionResult> DestroySetting([DataSourceRequest] DataSourceRequest request, TenantSettingViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.TenantSettings.First(n => n.TenantSettingId == viewModel.TenantSettingId);
            if (ModelState.IsValid)
            {
                db.TenantSettings.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
