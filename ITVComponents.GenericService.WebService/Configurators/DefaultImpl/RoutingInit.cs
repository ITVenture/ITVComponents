using ITVComponents.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Configurators.DefaultImpl
{
    public class RoutingInit : IServiceHostConfigurator, IPlugin
    {
        private bool useAreas;

        public RoutingInit(IWebHostStartup startup, bool useAreas)
        {
            startup.RegisterConfigurator(this);
            this.useAreas = useAreas;
        }

        public RoutingInit(IWebHostStartup startup) : this(startup,false)
        {

        }

        public string UniqueName { get; set; }
        public void ConfigureBuilder(WebApplicationBuilder builder)
        {
            builder.Services.AddRouting();
        }

        public void ConfigureApp(WebApplication app)
        {
            app.UseRouting();
            app.UseEndpoints(ue =>
            {
                if (useAreas)
                {
                    ue.MapControllerRoute(
                        name: "defaultWithAreas",
                        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
                }

                ue.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
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
