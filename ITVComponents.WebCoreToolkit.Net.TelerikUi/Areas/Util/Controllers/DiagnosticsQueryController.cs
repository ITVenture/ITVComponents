using System.Linq;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.MvcExtensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Areas.Util.Controllers
{
    [Authorize("HasPermission(DiagnosticsQueries.View,DiagnosticsQueries.Write)"), Area("Util")]
    public class DiagnosticsQueryController : Controller
    {
        private readonly SecurityContext db;

        public DiagnosticsQueryController(SecurityContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ParameterTable(int diagnosticsQueryId)
        {
            ViewData["diagnosticsQueryId"] = diagnosticsQueryId;
            ViewData["ParameterTypes"] = new SelectList(EnumHelper.DescribeEnum<QueryParameterTypes>(), "Value", "Description");
            return PartialView();
        }

        [HttpPost]
        public IActionResult Read([DataSourceRequest] DataSourceRequest request)
        {
            db.ShowAllTenants = true;
            return Json(db.DiagnosticsQueries.ToDataSourceResult(request, n => n.ToViewModel<DiagnosticsQuery, DiagnosticsQueryViewModel>(ApplyTenants)));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> Create([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = new DiagnosticsQuery();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryViewModel,DiagnosticsQuery>(model);
                db.DiagnosticsQueries.Add(model);
                SetTenants(viewModel.Tenants, model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<DiagnosticsQuery, DiagnosticsQueryViewModel>(ApplyTenants)}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> Destroy([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.DiagnosticsQueries.First(n => n.DiagnosticsQueryId== viewModel.DiagnosticsQueryId);
            if (ModelState.IsValid)
            {
                db.TenantDiagnosticsQueries.RemoveRange(model.Tenants);
                db.DiagnosticsQueryParameters.RemoveRange(model.Parameters);
                db.DiagnosticsQueries.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> Update([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.DiagnosticsQueries.First(n => n.DiagnosticsQueryId == viewModel.DiagnosticsQueryId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryViewModel,DiagnosticsQuery>(model, "", m => { return m.ElementType == null; });
                SetTenants(viewModel.Tenants, model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<DiagnosticsQuery, DiagnosticsQueryViewModel>(ApplyTenants)}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        public IActionResult ReadParameters([DataSourceRequest] DataSourceRequest request, int diagnosticsQueryId)
        {
            db.ShowAllTenants = true;
            return Json(db.DiagnosticsQueryParameters.Where(n => n.DiagnosticsQueryId == diagnosticsQueryId).ToDataSourceResult(request, n => n.ToViewModel<DiagnosticsQueryParameter, DiagnosticsQueryParameterViewModel>()));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> CreateParameter([DataSourceRequest] DataSourceRequest request, [FromQuery]int diagnosticsQueryId)
        {
            db.ShowAllTenants = true;
            var model = new DiagnosticsQueryParameter();
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryParameterViewModel,DiagnosticsQueryParameter>(model);
                model.DiagnosticsQueryId = diagnosticsQueryId;
                db.DiagnosticsQueryParameters.Add(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<DiagnosticsQueryParameter, DiagnosticsQueryParameterViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> DestroyParameter([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryParameterViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.DiagnosticsQueryParameters.First(n => n.DiagnosticsQueryParameterId == viewModel.DiagnosticsQueryParameterId);
            if (ModelState.IsValid)
            {
                db.DiagnosticsQueryParameters.Remove(model);
                await db.SaveChangesAsync();
            }

            return Json(await new[] {viewModel}.ToDataSourceResultAsync(request, ModelState));
        }

        [HttpPost]
        [Authorize("HasPermission(DiagnosticsQueries.Write)")]
        public async Task<IActionResult> UpdateParameter([DataSourceRequest] DataSourceRequest request, DiagnosticsQueryParameterViewModel viewModel)
        {
            db.ShowAllTenants = true;
            var model = db.DiagnosticsQueryParameters.First(n => n.DiagnosticsQueryParameterId == viewModel.DiagnosticsQueryParameterId);
            if (ModelState.IsValid)
            {
                await this.TryUpdateModelAsync<DiagnosticsQueryParameterViewModel,DiagnosticsQueryParameter>(model, "", m => { return m.ElementType == null; });
                await db.SaveChangesAsync();
            }

            return Json(await new[] {model.ToViewModel<DiagnosticsQueryParameter, DiagnosticsQueryParameterViewModel>()}.ToDataSourceResultAsync(request, ModelState));
        }

        private void ApplyTenants(DiagnosticsQuery original, DiagnosticsQueryViewModel vm)
        {
            vm.Tenants = (from t in original.Tenants select t.TenantId).ToArray();
        }

        private void SetTenants(int[] tenants, DiagnosticsQuery query)
        {
            var tmp = (from t in query.Tenants
                join nt in tenants on t.TenantId equals nt
                    into j
                from o in j.DefaultIfEmpty()
                where o == 0
                select t).ToArray();
            var tmp2 = (from t in tenants
                join ot in query.Tenants on t equals ot.TenantId
                    into j
                from n in j.DefaultIfEmpty()
                where n == null
                select t).ToArray();
            db.TenantDiagnosticsQueries.RemoveRange(tmp);
            db.TenantDiagnosticsQueries.AddRange(from t in tmp2 select new TenantDiagnosticsQuery(){TenantId = t, DiagnosticsQuery= query});
        }
    }
}
