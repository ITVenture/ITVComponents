using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Plugins.PluginServices;
using ITVComponents.WebCoreToolkit.InterProcessExtensions;
using ITVComponents.WebCoreToolkit.Net.PlugInServices;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.AspNetCore.Mvc;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewComponents
{
    public class PlugInAnalyzer:ViewComponent
    {
        private readonly IInjectablePlugin<WebPluginAnalyzer> localLoader;
        private readonly IInjectableProxy<ILoaderInterface> remoteLoader;

        public PlugInAnalyzer(IInjectablePlugin<WebPluginAnalyzer> localLoader = null, IInjectableProxy<ILoaderInterface> remoteLoader = null)
        {
            this.localLoader = localLoader;
            this.remoteLoader = remoteLoader;
        }
        public async Task<IViewComponentResult> InvokeAsync(bool local, string assemblyName)
        {
            ILoaderInterface target;
            if (local)
            {
                target = localLoader.Instance;
            }
            else
            {
                target = remoteLoader.Value;
            }

            if (target != null)
            {
                var tmp = target.DescribeAssembly(assemblyName);
                return View(new AssemblyDescriptionModel(assemblyName, tmp));
            }

            return View(new AssemblyDescriptionModel(assemblyName, new TypeDescriptor[] { }));
        }
    }
}
