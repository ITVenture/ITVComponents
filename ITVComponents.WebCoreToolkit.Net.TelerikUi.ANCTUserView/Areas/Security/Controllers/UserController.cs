using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.ViewModels;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.CustomUserProperty;
using User = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.User;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Areas.Security.Controllers
{
    [Authorize("HasPermission(Users.View,Users.Write),HasFeature(ITVAdminViews)"), Area("Security"), ConstructedGenericControllerConvention(ControllerName = "UserController")]
    public class UserController<TContext> : Controller
    where TContext:AspNetSecurityContext<TContext>
    {
        private readonly TContext db;

        private readonly bool isSysAdmin;

        public UserController(TContext db, IServiceProvider services)
        {
            this.db = db;
            if (!services.VerifyUserPermissions(new[] {EntityFramework.TenantSecurityShared.Helpers.ToolkitPermission.Sysadmin}))
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

        [Authorize("HasPermission(Users.Properties.View,Users.Properties.Write)")]
        public IActionResult PropertyTable(string userId)
        {
            ViewData["userId"] = userId;
            ViewData["propertyTypes"] = new SelectList(EnumHelper.DescribeEnum<CustomUserPropertyType>(),"Value","Description");
            return PartialView();
        }

        [Authorize("HasPermission(Users.Logins.View,Users.Logins.Write)")]
        public IActionResult LoginTable(string userId)
        {
            ViewData["userId"] = userId;
            return PartialView();
        }

        [Authorize("HasPermission(Users.Claims.View,Users.Claims.Write)")]
        public IActionResult ClaimTable(string userId)
        {
            ViewData["userId"] = userId;
            return PartialView();
        }

        [Authorize("HasPermission(Users.Tokens.View,Users.Tokens.Write)")]
        public IActionResult TokenTable(string userId)
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

                db.HideDisabledUsers = false;
                return Json((from u in db.Users
                    join t in db.TenantUsers on u.Id equals t.UserId
                    where t.TenantId == tenantId
                    select new UserViewModel
                    {
                        Id = t.TenantUserId.ToString(),
                        UserName = u.UserName,
                        AuthenticationTypeId = u.AuthenticationTypeId,
                        TenantId = tenantId,
                        Enabled = t.Enabled??true
                    }).ToDataSourceResult(request, ModelState));
            }
            else
            {
                return Json((from p in db.Users
                    join tu in db.TenantUsers on p.Id equals tu.UserId
                    join ro in db.SecurityRoles on tu.TenantId equals ro.TenantId
                    join r in db.TenantUserRoles on new {tu.TenantUserId, ro.RoleId} equals new {r.TenantUserId, r.RoleId} into lj
                    from s in lj.DefaultIfEmpty()
                    where ro.RoleId == roleId.Value && tu.TenantId == tenantId
                    
                    select new UserViewModel()
                    {
                        Id = tu.TenantUserId.ToString(),
                        UserName= p.UserName,
                        RoleId = roleId,
                        Assigned = s != null,
                        UniQUID = $"{p.Id}_{roleId}",
                        TenantId = tenantId
                    }).ToDataSourceResult(request, ModelState));
            }
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
        [Authorize("HasPermission(Users.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, UserViewModel viewModel, [FromQuery]int? tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId;
            }
            if (tenantId == null && isSysAdmin)
            {
                var model = db.Users.First(n => n.Id == viewModel.Id);
                if (ModelState.IsValid)
                {
                    db.Users.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
            }

            if (int.TryParse(viewModel.Id, out var tuid))
            {
                db.HideDisabledUsers = false;
                var tuModel = db.TenantUsers.First(n => n.TenantUserId == tuid && n.TenantId == tenantId);
                if (ModelState.IsValid)
                {
                    db.TenantUsers.Remove(tuModel);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            return BadRequest("Illegal User-Id-Format");
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

            var isTenantUser = int.TryParse(viewModel.Id, out var tuid);
            
            if (viewModel.TenantId == null && viewModel.RoleId == null && HttpContext.RequestServices.VerifyUserPermissions(new []{"Users.Write"}) && isSysAdmin)
            {
                var model = db.Users.First(n => n.Id == viewModel.Id);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<UserViewModel,User>(model, "", m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<User, UserViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.RoleId != null && isTenantUser && HttpContext.RequestServices.VerifyUserPermissions(new []{"Users.AssignRole"}))
            {
                var usr = db.TenantUsers.FirstOrDefault(n => n.TenantUserId == tuid && n.TenantId == tenantId);
                if (usr != null)
                {
                    var model = db.TenantUserRoles.FirstOrDefault(n => n.TenantUserId == tuid && n.RoleId == viewModel.RoleId);
                    if ((model == null) == viewModel.Assigned)
                    {
                        if (model == null)
                        {
                            db.TenantUserRoles.Add(new UserRole
                            {
                                TenantUserId = tuid,
                                RoleId = viewModel.RoleId.Value
                            });
                        }
                        else
                        {
                            db.TenantUserRoles.Remove(model);
                        }

                        await db.SaveChangesAsync();
                    }

                    return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
                }
            }
            else if (viewModel.RoleId == null && isTenantUser &&
                     HttpContext.RequestServices.VerifyUserPermissions(new[]
                         { ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin }))
            {
                db.HideDisabledUsers = false;
                var model = db.TenantUsers.First(n => n.TenantUserId == tuid);
                model.Enabled = viewModel.Enabled;
                await db.SaveChangesAsync();
                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        /*************************************
         * Props
         ************************************/
        [Authorize("HasPermission(Users.Properties.View,Users.Properties.Write)")]
        public IActionResult ReadProperties([DataSourceRequest] DataSourceRequest request, [FromQuery] string userId)
        {
            if (isSysAdmin)
            {
                return Json(db.UserProperties.Where(n => n.UserId == userId).ToDataSourceResult(request, ModelState, p => p.ToViewModel<CustomUserProperty, CustomUserPropertyViewModel>()));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Properties.Write)")]
        public async Task<IActionResult> CreateProperty([DataSourceRequest] DataSourceRequest request, [FromQuery] string userId)
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

                return Json(await new[] { model.ToViewModel<CustomUserProperty, CustomUserPropertyViewModel>() }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
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

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
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

        /*************************************
         * Logins
         ************************************/
        [Authorize("HasPermission(Users.Logins.View,Users.Logins.Write)")]
        public IActionResult ReadLogins([DataSourceRequest] DataSourceRequest request, [FromQuery] string userId)
        {
            if (isSysAdmin)
            {
                return Json(db.UserLogins.Where(n => n.UserId == userId).ToDataSourceResult(request, ModelState, p => p.ToViewModel<IdentityUserLogin<string>, UserLoginViewModel>()));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Logins.Write)")]
        public async Task<IActionResult> DestroyLogin([DataSourceRequest] DataSourceRequest request, UserLoginViewModel viewModel)
        {
            if (isSysAdmin)
            {
                var model = db.UserLogins.First(n => n.LoginProvider == viewModel.LoginProvider && n.ProviderKey == viewModel.ProviderKey && n.UserId == viewModel.UserId);
                if (ModelState.IsValid)
                {
                    db.UserLogins.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        /*************************************
         * Tokens
         ************************************/
        [Authorize("HasPermission(Users.Tokens.View,Users.Tokens.Write)")]
        public IActionResult ReadTokens([DataSourceRequest] DataSourceRequest request, [FromQuery] string userId)
        {
            if (isSysAdmin)
            {
                return Json(db.UserTokens.Where(n => n.UserId == userId).ToDataSourceResult(request, ModelState, p => p.ToViewModel<IdentityUserToken<string>, UserTokenViewModel>()));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Tokens.Write)")]
        public async Task<IActionResult> DestroyToken([DataSourceRequest] DataSourceRequest request, UserTokenViewModel viewModel)
        {
            if (isSysAdmin)
            {
                var model = db.UserTokens.First(n => n.LoginProvider == viewModel.LoginProvider && n.Name == viewModel.Name && n.UserId == viewModel.UserId);
                if (ModelState.IsValid)
                {
                    db.UserTokens.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        /*************************************
         * Claims
         ************************************/
        [Authorize("HasPermission(Users.Claims.View,Users.Claims.Write)")]
        public IActionResult ReadClaims([DataSourceRequest] DataSourceRequest request, [FromQuery] string userId)
        {
            if (isSysAdmin)
            {
                return Json(db.UserClaims.Where(n => n.UserId == userId).ToDataSourceResult(request, ModelState, p => p.ToViewModel<IdentityUserClaim<string>, UserClaimViewModel>()));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Claims.Write)")]
        public async Task<IActionResult> CreateClaim([DataSourceRequest] DataSourceRequest request, [FromQuery] string userId)
        {
            if (isSysAdmin)
            {
                var model = new IdentityUserClaim<string>();
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<UserClaimViewModel, IdentityUserClaim<string>>(model);
                    model.UserId = userId;
                    db.UserClaims.Add(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] { model.ToViewModel<IdentityUserClaim<string>, UserClaimViewModel>() }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Claims.Write)")]
        public async Task<IActionResult> DestroyClaim([DataSourceRequest] DataSourceRequest request, UserClaimViewModel viewModel)
        {
            if (isSysAdmin)
            {
                var model = db.UserClaims.First(n => n.Id == viewModel.Id);
                if (ModelState.IsValid)
                {
                    db.UserClaims.Remove(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }

        [HttpPost]
        [Authorize("HasPermission(Users.Claims.Write)")]
        public async Task<IActionResult> UpdateClaim([DataSourceRequest] DataSourceRequest request, UserClaimViewModel viewModel)
        {
            if (isSysAdmin)
            {
                var model = db.UserClaims.First(n => n.Id == viewModel.Id);
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<UserClaimViewModel, IdentityUserClaim<string>>(model);
                    await db.SaveChangesAsync();
                }

                return Json(await new[] { model.ToViewModel<IdentityUserClaim<string>, UserClaimViewModel>() }.ToDataSourceResultAsync(request, ModelState));
            }

            return Unauthorized();
        }
    }
}
