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

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Tenants.View,Tenants.Write,Tenants.AssignUser,Tenants.WriteSettings)"), Area("Security"), ConstructedGenericControllerConvention(ControllerName = "TenantController")]
    public class TenantControllerStruct<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty, TContext> : Controller
        where TRole : Role<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TPermission : Permission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TUserRole : UserRole<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TRolePermission : RolePermission<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>
        where TTenantUser : TenantUser<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser>, new()
        where TNavigationMenu : NavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TWidgetParam : DashboardParam<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserWidget : UserWidget<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TUserId: struct
        where TContext : DbContext, ISecurityContext<TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TUserWidget, TUserProperty>
    {
        private readonly TContext db;

        private readonly IUserExpressionHelper<TUserId, TUser, TTenantUser> expressionHelper;

        public TenantControllerStruct(TContext db, IUserExpressionHelper<TUserId, TUser, TTenantUser> expressionHelper, IServiceProvider services)
        {
            this.db = db;
            this.expressionHelper = expressionHelper;
            if (!services.VerifyUserPermissions(new[] { EntityFramework.TenantSecurityShared.Helpers.ToolkitPermission.Sysadmin}))
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
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] TUserId? userId)
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
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, TenantViewModelS<TUserId> viewModel)
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
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, TenantViewModelS<TUserId> viewModel)
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
                var user = db.Users.First(expressionHelper.EqualsUserId((TUserId)viewModel.UserId));
                var model = db.TenantUsers.FirstOrDefault(expressionHelper.EqualsUserTenantId((TUserId)viewModel.UserId,viewModel.TenantId));
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.TenantUsers.Add(new TTenantUser()
                        {
                            TenantId = viewModel.TenantId,
                            UserId = expressionHelper.UserId(user)
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
