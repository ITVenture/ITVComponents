using ITVComponents.DataAccess.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel;
using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
            return Json(db.SystemLog.ToDataSourceResult(request, n => n.ToViewModel<SystemEvent, SystemEventViewModel>(
                (e, m) =>
                {
                    var iconClass = "";
                    var primaryColor = "";
                    var secondaryColor = "";
                    var backColor = "";
                    var shadowColor = "";
                    if (e.LogLevel == LogLevel.Trace)
                    {
                        iconClass = "fa-duotone fa-glasses";
                        primaryColor = "#362b2c";
                        secondaryColor = "#fcd2d5";
                        backColor = "#362b2c";
                        shadowColor = "#fcd2d5";

                    }
                    else if (e.LogLevel == LogLevel.Debug)
                    {
                        iconClass = "fa-duotone fa-bug";
                        primaryColor = "#324d05";
                        secondaryColor = "#000000";
                        backColor = "#000000";
                        shadowColor = "#324d05";
                    }
                    else if (e.LogLevel == LogLevel.Information)
                    {
                        iconClass = "fa-duotone fa-info-circle";
                        primaryColor = "#000000";
                        secondaryColor = "#92a5f0";
                        backColor = "#92a5f0";
                        shadowColor = "#000000";
                    }
                    else if (e.LogLevel == LogLevel.Warning)
                    {
                        iconClass = "fa-duotone fa-exclamation-circle";
                        primaryColor = "#000000";
                        secondaryColor = "#f2e296";
                        backColor = "#c09854";
                        shadowColor = "#1a3c4d";
                    }
                    else if (e.LogLevel == LogLevel.Error)
                    {
                        iconClass = "fa-duotone fa-times-circle";
                        primaryColor = "#000000";
                        secondaryColor = "#bf6363";
                        backColor = "#bf6363";
                        shadowColor = "#000000";
                    }
                    else if (e.LogLevel == LogLevel.Critical)
                    {
                        iconClass = "fa-duotone fa-skull-crossbones";
                        primaryColor = "#000000";
                        secondaryColor = "red";
                        backColor = "#red";
                        shadowColor = "#000000";
                    }
                    else if (e.LogLevel == LogLevel.None)
                    {
                        iconClass = "fa-duotone fa-question-circle";
                        primaryColor = "#000000";
                        secondaryColor = "#faffa3";
                        backColor = "#faffa3";
                        shadowColor = "#000000";
                    }
                    var iconStyle = $"--fa-primary-color:{primaryColor};--fa-secondary-color:{secondaryColor}";
                    var popupStyle = $"background-color:{backColor};text-shadow:0 -1px 0 {shadowColor};color:white";
                    m.LogInfo = new
                    {
                        IconClass = iconClass,
                        IconStyle = iconStyle,
                        PopupStyle = popupStyle,
                        e.Message
                    };
                })));
        }
    }
}
