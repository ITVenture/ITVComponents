using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.GenericService.WebService.Services;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Configurators.DefaultImpl
{
    public class AutoPluginsServices : IServiceHostConfigurator
    {
        public AutoPluginsServices(IWebHostStartup startup)
        {
            startup.RegisterConfigurator(this);
        }

        public void ConfigureBuilder(WebApplicationBuilder builder)
        {
            builder.Services.AddScoped(typeof(IInjectablePlugin<>), typeof(SimplePluginInjector<>));
        }

        public void ConfigureApp(WebApplication app)
        {
        }
    }
}
