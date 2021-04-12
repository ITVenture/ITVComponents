using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.DynamicData;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class DynamicTable:ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(string name, string sourceContextName, string controllerName, ICollection<TableColumnDefinition> tableDef, bool create = true, bool update = true, bool delete = true, object customRouteData = null)
        {
            ViewData["name"] = CustomActionHelper.RandomName(name);
            ViewData["controllerName"] = controllerName;
            ViewData["create"] = create;
            ViewData["update"] = update;
            ViewData["delete"] = delete;
            ViewData["customRouteData"] = customRouteData;
            ViewData["contextForFk"] = sourceContextName;
            return View(tableDef);
        }
    }
}
