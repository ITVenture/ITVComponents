using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.GenericService.WebService.Services;
using ITVComponents.Plugins;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Configurators.DefaultImpl
{
    public class FactoryExposeInit : IServiceHostConfigurator, IPlugin
    {
        private readonly PluginFactory factory;
        public string UniqueName { get; set; }

        public FactoryExposeInit(IWebHostStartup webHost, PluginFactory factory)
        {
            webHost.RegisterConfigurator(this);
            this.factory = factory;
        }
        
        public void ConfigureBuilder(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton(factory);
            builder.Services.AddScoped<IWebPluginHelper, ServicePluginHelper>();
            builder.Services.UseInjectablePlugins(o => o.CheckForAreaPrefixedNames = false);
        }

        public void ConfigureApp(WebApplication app)
        {
        }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disposed;
    }
}
