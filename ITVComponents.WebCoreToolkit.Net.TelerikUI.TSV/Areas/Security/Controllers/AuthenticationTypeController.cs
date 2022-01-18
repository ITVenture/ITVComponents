using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Authorize("HasPermission(AuthenticationTypes.View,AuthenticationTypes.Write)"), Area("Security")]
    public class AuthenticationTypeController : Controller
    {
       private readonly SecurityContext db;

        public AuthenticationTypeController(SecurityContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult AuthClaims(int authenticationTypeId)
        {
            ViewData["authenticationTypeId"] = authenticationTypeId;
            return View();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.AuthenticationTypes.ToDataSourceResult(request, n => n.ToViewModel<AuthenticationType, AuthenticationTypeViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(AuthenticationTypes.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request)
        {
            var model = new AuthenticationType();
            if (ModelState.IsValid)
            {
                
                await this.TryUpdateModelAsync<AuthenticationTypeViewModel,AuthenticationType>(model);
                db.AuthenticationTypes.Add(model);

                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<AuthenticationType, AuthenticationTypeViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(AuthenticationTypes.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, AuthenticationTypeViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.AuthenticationTypes.First(n => n.AuthenticationTypeId== viewModel.AuthenticationTypeId);
            if (ModelState.IsValid)
            {
                db.AuthenticationTypes.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(AuthenticationTypes.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, AuthenticationTypeViewModel viewModel)
        {
            var model = db.AuthenticationTypes.First(n => n.AuthenticationTypeId== viewModel.AuthenticationTypeId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<AuthenticationTypeViewModel,AuthenticationType>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<AuthenticationType, AuthenticationTypeViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadClaims([DataSourceRequest] DataSourceRequest request, [FromQuery]int authenticationTypeId)
        {
            return Json(db.AuthenticationClaimMappings.Where(n => n.AuthenticationTypeId == authenticationTypeId).ToDataSourceResult(request, n => n.ToViewModel<AuthenticationClaimMapping, AuthenticationClaimMappingViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(AuthenticationTypes.Write)")]
        public async Task<IActionResult> CreateClaim([DataSourceRequest] DataSourceRequest request, [FromQuery]int authenticationTypeId)
        {
            var model = new AuthenticationClaimMapping();
            if (ModelState.IsValid)
            {

                await this.TryUpdateModelAsync<AuthenticationClaimMappingViewModel, AuthenticationClaimMapping>(model);
                model.AuthenticationTypeId = authenticationTypeId;
                db.AuthenticationClaimMappings.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<AuthenticationClaimMapping, AuthenticationClaimMappingViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(AuthenticationTypes.Write)")]
        public async Task<IActionResult> DestroyClaim([DataSourceRequest] DataSourceRequest request, AuthenticationClaimMappingViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.AuthenticationClaimMappings.First(n => n.AuthenticationClaimMappingId == viewModel.AuthenticationClaimMappingId);
            if (ModelState.IsValid)
            {
                db.AuthenticationClaimMappings.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] { viewModel }.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(AuthenticationTypes.Write)")]
        public async Task<IActionResult> UpdateClaim([DataSourceRequest] DataSourceRequest request, AuthenticationClaimMappingViewModel viewModel)
        {
            var model = db.AuthenticationClaimMappings.First(n => n.AuthenticationClaimMappingId == viewModel.AuthenticationClaimMappingId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<AuthenticationClaimMappingViewModel, AuthenticationClaimMapping>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] { model.ToViewModel<AuthenticationClaimMapping, AuthenticationClaimMappingViewModel>() }.ToDataSourceResultAsync(request, ModelState));
        }
    }
}
