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
using TextsAndMessagesHelper = ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Resources.TextsAndMessagesHelper;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(Roles.Write,Roles.AssignPermission,Roles.User,Roles.View)"), Area("Security")]
    public class RoleController : Controller
    {
       private readonly SecurityContext db;
       private bool isSysAdmin;

        public RoleController(SecurityContext db, IServiceProvider services)
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
            if (db.CurrentTenantId == null)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_RC_Tenant_Bound_User_Required_For_Index);
            }
            
            return View(db.CurrentTenantId.Value);
        }

        public IActionResult RoleTable(int tenantId, int? permissionId, int? tenantUserId)
        {
            ViewData["tenantId"] = tenantId;
            ViewData["permissionId"] = permissionId;
            ViewData["tenantUserId"] = tenantUserId;
            return PartialView();
        }

        public IActionResult RoleDetails(int tenantId, int roleId)
        {
            var tmp = db.Roles.First(n => n.RoleId == roleId && n.TenantId == tenantId);
            return View(tmp.ToViewModel<Role, RoleViewModel>());
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, int tenantId, [FromQuery]int? permissionId, [FromQuery]int? tenantUserId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
            }
            
            if (tenantUserId == null && permissionId == null)
            {
                return Json(db.Roles.Where(n => n.TenantId == tenantId).ToDataSourceResult(request, n => n.ToViewModel<Role, RoleViewModel>((m,v) => v.Editable = !m.IsSystemRole || isSysAdmin)));
            }
            else if (permissionId != null)
            {
                return Json((from p in db.Roles
                    join r in db.RolePermissions on new {p.RoleId, p.TenantId, PermissionId = permissionId.Value} equals new {r.RoleId, r.TenantId, r.PermissionId} into lj
                    from s in lj.DefaultIfEmpty()
                    where p.TenantId == tenantId && (!p.IsSystemRole || isSysAdmin)
                    select new RoleViewModel
                    {
                        RoleId = p.RoleId,
                        TenantId = tenantId,
                        PermissionId = permissionId,
                        Assigned = s != null,
                        UniQUID = $"{p.RoleId}_{tenantId}_{permissionId}",
                        RoleName=p.RoleName
                    }).ToDataSourceResult(request, ModelState));
            }
            else
            {
                return Json((from p in db.Roles
                    join r in db.UserRoles on new {p.RoleId, TenantUserId = tenantUserId.Value} equals new {r.RoleId, r.TenantUserId} into lj
                    from s in lj.DefaultIfEmpty()
                    where p.TenantId == tenantId
                    select new RoleViewModel
                    {
                        RoleId = p.RoleId,
                        UserId = tenantUserId,
                        RoleName = p.RoleName,
                        Assigned = s != null,
                        TenantId = tenantId,
                        UniQUID = $"{p.RoleId}_{tenantUserId}"
                    }).ToDataSourceResult(request, ModelState));
            }
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request,[FromQuery] int tenantId)
        {
            if (!isSysAdmin)
            {
                tenantId = db.CurrentTenantId.Value;
            }
            var model = new Role();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<RoleViewModel,Role>(model);
                if (model.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
                }
                
                model.TenantId = tenantId;
                db.Roles.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<Role, RoleViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, RoleViewModel viewModel)
        {
            var model = db.Roles.First(n => n.RoleId== viewModel.RoleId);
            if (model.IsSystemRole && !isSysAdmin)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
            }
            
            if (ModelState.IsValid)
            {
                db.Roles.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(Roles.Write,Roles.AssignPermission,Roles.AssignUser)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, RoleViewModel viewModel)
        {
            if (viewModel.PermissionId == null && viewModel.UserId == null && HttpContext.RequestServices.VerifyUserPermissions(new []{"Roles.Write"}))
            {
                var model = db.Roles.First(n => n.RoleId== viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if (model.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
                }
                
                if (ModelState.IsValid)
                {
                    await this.TryUpdateModelAsync<RoleViewModel,Role>(model, "", m => { return m.ElementType == null; });
                    await db.SaveChangesAsync();
                }

                return Json(await new[] {model.ToViewModel<Role, RoleViewModel>()}.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.PermissionId != null && HttpContext.RequestServices.VerifyUserPermissions(new []{"Roles.AssignPermission"}))
            {
                var role = db.Roles.First(n => n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if (role.IsSystemRole && !isSysAdmin)
                {
                    return BadRequest(TextsAndMessagesHelper.IWCN_RC_Must_Be_Sysadmin_To_Edit_Or_Create_SysRole);
                }
                
                var model = db.RolePermissions.FirstOrDefault(n => n.PermissionId == viewModel.PermissionId && n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.RolePermissions.Add(new RolePermission
                        {
                            PermissionId = viewModel.PermissionId.Value,
                            RoleId = viewModel.RoleId,
                            TenantId = viewModel.TenantId
                        });
                    }
                    else
                    {
                        db.RolePermissions.Remove(model);
                    }

                    await db.SaveChangesAsync();
                }

                return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
            }
            else if (viewModel.UserId != null && HttpContext.RequestServices.VerifyUserPermissions(new[] {"Roles.AssignUser"}))
            {
                var model = db.UserRoles.FirstOrDefault(n => n.TenantUserId == viewModel.UserId && n.RoleId == viewModel.RoleId && n.User.TenantId == viewModel.TenantId);
                var role = db.Roles.First(n => n.RoleId == viewModel.RoleId && n.TenantId == viewModel.TenantId);
                if ((model == null) == viewModel.Assigned)
                {
                    if (model == null)
                    {
                        db.UserRoles.Add(new UserRole
                        {
                            TenantUserId = viewModel.UserId.Value,
                            RoleId = viewModel.RoleId
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

            return Unauthorized();
        }
    }
}
