using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Util.Controllers
{
    [Authorize("HasPermission(SystemLog.View),HasFeature(ITVAdminViews)"), Area("Util")]
    public class SystemLogController:Controller
    {
        private readonly IBaseTenantContext db;

        public SystemLogController(IBaseTenantContext db)
        {
            this.db = db;
            db.ShowAllTenants = true;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ReadLog ([DataSourceRequest] DataSourceRequest request)
        {
            return Json(db.SystemLog.ToDataSourceResult(request, n => n.ToViewModel<SystemEvent, SystemEventViewModel>()));
        }
    }
}
