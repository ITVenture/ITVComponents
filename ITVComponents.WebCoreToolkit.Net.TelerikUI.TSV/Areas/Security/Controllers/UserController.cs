using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Users.View,Users.Write)"), Area("Security")]
    public class UserController : Controller
    {
        private readonly SecurityContext db;

        private readonly bool isSysAdmin;

        public UserController(SecurityContext db, IServiceProvider services)
        {
            this.db = db;
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
            
            ViewData["tenantId"]= tenantId;
            return View();
        }

        public IActionResult 
            UserTable(int? roleId, int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            ViewData["roleId"] = roleId;
            ViewData["tenantId"] = tenantId;
            return PartialView();
        }

        [Authorize("HasPermission(Users.Properties.View)")]
        public IActionResult PropertyTable(int userId)
        {
            ViewData["userId"] = userId;
            return PartialView();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request,[FromQuery] int? roleId, [FromQuery]int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            
            if (roleId == null)
            {
                if (tenantId == null && isSysAdmin)
                {
                    return Json(db.Users.ToDataSourceResult(request, ModelState, n => n.ToViewModel<User, UserViewModel>()));
                }

                return Json((from u in db.Users
                    join t in db.TenantUsers on u.UserId equals t.UserId
                    where t.TenantId == tenantId
                    select new UserViewModel
                    {
                        UserId = t.TenantUserId,
                        UserName = u.UserName,
                        AuthenticationTypeId = u.AuthenticationTypeId,
                        TenantId = tenantId
                    }).ToDataSourceResult(request, ModelState));
            }
            else
            {
                return Json((from p in db.Users
                    join tu in db.TenantUsers on p.UserId equals tu.UserId
                    join ro in db.Roles on tu.TenantId equals ro.TenantId
                    join r in db.UserRoles on new {tu.TenantUserId, ro.RoleId} equals new {r.TenantUserId, r.RoleId} into lj
                    from s in lj.DefaultIfEmpty()
                    where ro.RoleId == roleId.Value && tu.TenantId == tenantId
                    
                    select new UserViewModel()
                    {
                        UserId= tu.TenantUserId,
                        UserName= p.UserName,
                        RoleId = roleId,
                        Assigned = s != null,
                        UniQUID = $"{p.UserId}_{roleId}",
                        TenantId = tenantId
                    }).ToDataSourceResult(request, ModelState));
            }
        }

        [Authorize("HasPermission(Users.Properties.View,Users.Properties.Write)")]
        public IActionResult ReadProperties([DataSourceRequest] DataSourceRequest request,[FromQuery] int userId)
        {
            if (isSysAdmin)
            {
                return Json(db.UserProperties.Where(n => n.UserId == userId).ToDataSourceResult(request, ModelState, p => p.ToViewModel<CustomUserProperty, CustomUserPropertyViewModel>()));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            if (isSysAdmin)
            {
                var model = new User();
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<UserViewModel, User>(model);
                    db.Users.Add(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<User, UserViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Properties.Write)")]
        public async Task<IActionResult> CreateProperty([DataSourceRequest] DataSourceRequest request,[FromQuery] int userId)
        {
            if (isSysAdmin)
            {
                var model = new CustomUserProperty();
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<CustomUserPropertyViewModel, CustomUserProperty>(model);
                    model.UserId = userId;
                    db.UserProperties.Add(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<CustomUserProperty, CustomUserPropertyViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, UserViewModel viewModel, [FromQuery]int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            if (tenantId == null && isSysAdmin)
            {
                var model = db.Users.First(n => n.UserId == viewModel.UserId);
                if (ModelState.IsValid)
                {
                    db.Users.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
            }

            var tuModel = db.TenantUsers.First(n => n.TenantUserId == viewModel.UserId && n.TenantId == tenantId);
            if (ModelState.IsValid)
            {
                db.TenantUsers.Remove(tuModel);
                await db.SaveChangesAsync();
            }
            
            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Properties.Write)")]
        public async Task<IActionResult> DestroyProperty([DataSourceRequest] DataSourceRequest request, CustomUserPropertyViewModel viewModel)
        {
            if (isSysAdmin)
            {
                var model = db.UserProperties.First(n => n.CustomUserPropertyId == viewModel.CustomUserPropertyId);
                if (ModelState.IsValid)
                {
                    db.UserProperties.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Write,Users.AssignRole)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, UserViewModel viewModel, [FromQuery]int? roleId, [FromQuery]int? tenantId)
        {
            tenantId ??= viewModel.TenantId;
            if (!isSysAdmin)
            {

                tenantId = db.CurrentTenantId;
            }
            
            if (viewModel.RoleId == null && HttpContext.RequestServices.VerifyUserPermissions(new []{"Users.Write"}) && isSysAdmin)
            {
                var model = db.Users.First(n => n.UserId == viewModel.UserId);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<UserViewModel,User>(model, "", m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<User, UserViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.RoleId != null && HttpContext.RequestServices.VerifyUserPermissions(new []{"Users.AssignRole"}))
            {
                var usr = db.TenantUsers.FirstOrDefault(n => n.TenantUserId == viewModel.UserId && n.TenantId == tenantId);
                if (usr != null)
                {
                    var model = db.UserRoles.FirstOrDefault(n => n.TenantUserId == viewModel.UserId && n.RoleId == viewModel.RoleId);
                    if ((model == null) == viewModel.Assigned)
                    {
                        if (model == null)
                        {
                            db.UserRoles.Add(new UserRole
                            {
                                TenantUserId = viewModel.UserId,
                                RoleId = viewModel.RoleId.Value
                            });
                        }
                        else
                        {
                            db.UserRoles.Remove(model);
                        }

                        await db.SaveChangesAsync();
                    }

                    return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
                }
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Properties.Write)")]
        public async Task<IActionResult> UpdateProperty([DataSourceRequest] DataSourceRequest request, CustomUserPropertyViewModel viewModel)
        {
            if (isSysAdmin)
            {
                var model = db.UserProperties.First(n => n.CustomUserPropertyId == viewModel.CustomUserPropertyId);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<CustomUserPropertyViewModel, CustomUserProperty>(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<CustomUserProperty, CustomUserPropertyViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }
    }
}
