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
    [Authorize(Policy="HasPermission(Navigation.View,Navigation.Write)"), Area("Security")]
    public class NavigationController : Controller
    {
        private readonly SecurityContext db;

        public NavigationController(SecurityContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            //ViewData["Permissions"] = db.ReadForeignKey("Permissions").Cast<ForeignKeyData<int>>().ToList();
            return View();
        }

        public IActionResult NavTable(int? parentId)
        {
            ViewData["parentId"] = parentId;
            //ViewData["Permissions"] = db.ReadForeignKey("Permissions").Cast<ForeignKeyData<int>>().ToList();
            return PartialView();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request, [FromQuery]int? parentId)
        {
            db.ShowAllTenants = true;
            return Json(db.Navigation.Where(n => n.ParentId == parentId).ToDataSourceResult(request, n => n.ToViewModel<NavigationMenu, NavigationMenuViewModel>(ApplyTenants)));
        }

        [HttpPost]
        [Authorize(Policy="HasPermission(Navigation.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, [FromQuery]int? parentId, NavigationMenuViewModel viewModel)
        {
            var model = new NavigationMenu();
            if (ModelState.IsValid)
            {
                var order = db.Navigation.Where(n => n.ParentId == parentId).Max(n => n.SortOrder) ?? 0;
                await this.TryUpdateModelAsync<NavigationMenuViewModel,NavigationMenu>(model);
                model.ParentId = parentId;
                model.SortOrder = order + 1;
                db.Navigation.Add(model);
                SetTenants(viewModel.Tenants, model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<NavigationMenu, NavigationMenuViewModel>(ApplyTenants)}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize(Policy="HasPermission(Navigation.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, NavigationMenuViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.Navigation.First(n => n.NavigationMenuId == viewModel.NavigationMenuId);
            if (ModelState.IsValid)
            {
                db.TenantNavigation.RemoveRange(model.Tenants);
                db.Navigation.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize(Policy="HasPermission(Navigation.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, NavigationMenuViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.Navigation.First(n => n.NavigationMenuId == viewModel.NavigationMenuId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<NavigationMenuViewModel,NavigationMenu>(model, "", m =>
                {
                    return m.ElementType == null;
                });
                SetTenants(viewModel.Tenants, model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<NavigationMenu, NavigationMenuViewModel>(ApplyTenants)}.ToDataSourceResultAsync(request, ModelState));
        }

        private void ApplyTenants(NavigationMenu original, NavigationMenuViewModel vm)
        {
            vm.Tenants = (from t in original.Tenants select t.TenantId).ToArray();
        }

        private void SetTenants(int[] tenants, NavigationMenu menu)
        {
            var tmp = (from t in menu.Tenants
                join nt in tenants on t.TenantId equals nt
                    into j
                from o in j.DefaultIfEmpty()
                where o == 0
                select t).ToArray();
            var tmp2 = (from t in tenants
                join ot in menu.Tenants on t equals ot.TenantId
                    into j
                from n in j.DefaultIfEmpty()
                where n == null
                select t).ToArray();
            db.TenantNavigation.RemoveRange(tmp);
            db.TenantNavigation.AddRange(from t in tmp2 select new TenantNavigationMenu {TenantId = t, NavigationMenu = menu});
        }
    }
}
