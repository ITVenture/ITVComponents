using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class ShowTable:ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(IDictionary<string,object> customViewData)
        {
            foreach (var vd in customViewData)
            {
                ViewData[vd.Key] = vd.Value;
            }

            return View();
        }
    }
}
