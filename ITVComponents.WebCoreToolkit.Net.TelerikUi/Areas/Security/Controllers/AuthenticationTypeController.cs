using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Areas.Security.Controllers
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
    }
}
