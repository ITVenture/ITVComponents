using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Data;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewComponents
{
    public class ModuleActivation:ViewComponent
    {
        public ModuleActivation()
        {
        }

        public async Task<IViewComponentResult> InvokeAsync(string moduleName, string displayName, string customRenderPage, ModuleParameterTemplate [] arguments = null)
        {
            
            var model = new ModuleTemplateModel{ ModuleName = moduleName, DisplayName=displayName };
            model.Controls = arguments;
            return View(customRenderPage ?? "Default", model);
        }
    }
}
