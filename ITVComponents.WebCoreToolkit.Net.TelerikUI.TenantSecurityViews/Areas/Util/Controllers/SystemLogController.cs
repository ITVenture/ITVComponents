using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(SystemLog.View)"), Area("Util")]
    public class SystemLogController:Controller
    {
        private readonly SecurityContext db;

        public SystemLogController(SecurityContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ReadLog ([DataSourceRequest] DataSourceRequest request)
        {
            db.ShowAllTenants = true;
            return Json(db.SystemLog.ToDataSourceResult(request, n => n.ToViewModel<SystemEvent, SystemEventViewModel>()));
        }
    }
}
