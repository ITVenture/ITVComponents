using Microsoft.AspNetCore.Mvc;
using TextsAndMessagesHelper = ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Resources.TextsAndMessagesHelper;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Areas.Security.Controllers
{
    [Area("Security")]
    public class TenantPermissionController : Controller
    {
        public IActionResult Index(int? permissionId, int? roleId)
        {
            if (permissionId == null && roleId == null)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_TPC_Permission_Or_Role_Selection);
            }

            if (permissionId != null && roleId != null)
            {
                return BadRequest(TextsAndMessagesHelper.IWCN_TPC_Permission_Or_Role_Selection);
            }

            ViewData["permissionId"] = permissionId;
            ViewData["roleId"] = roleId;
            return PartialView();
        }
    }
}
