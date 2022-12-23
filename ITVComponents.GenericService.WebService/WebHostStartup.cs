using ITVComponents.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.GenericService.WebService.Configurators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService
{
    public class WebHostStartup: IPlugin, IDeferredInit,IWebHostStartup
    {
        private bool ownsApp;
        private WebApplication app;
        private List<IServiceHostConfigurator> configurators = new();
        private readonly string hostAddresses;
        private readonly string basePath;
        private static WebHostStartup instance;
        public string UniqueName { get; set; }
        public bool Initialized { get; private set; }
        public bool ForceImmediateInitialization => false;

        private WebHostStartup()
        {
            if (instance != null)
            {
                throw new InvalidOperationException("Can have only one active Instance of WebHostStartup at a time");
            }

            instance = this;
        }

        public WebHostStartup(string hostAddresses):this()
        {
            this.hostAddresses = hostAddresses;
            WebAppBuilder = WebApplication.CreateBuilder();
            ownsApp = true;
        }

        public WebHostStartup(WebApplicationBuilder builder)
        {
            this.WebAppBuilder = builder;
            ownsApp = false;
        }

        public void Initialize()
        {
            if (!Initialized)
            {
                SetDefaults();
                configurators.ForEach(n => n.ConfigureBuilder(WebAppBuilder));
                if (ownsApp)
                {
                    app = WebAppBuilder.Build();
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        app.UsePathBase(new PathString(basePath));
                    }

                    InitializeApp(app);
                    app.RunAsync();
                }

                Initialized = true;
            }
        }

        public WebApplicationBuilder WebAppBuilder { get; }
        public void RegisterConfigurator(IServiceHostConfigurator configurator)
        {
            configurators.Add(configurator);
        }

        public void InitializeApp(WebApplication app)
        {
            configurators.ForEach(n => n.ConfigureApp(app));
        }

        public void Dispose()
        {
            if (ownsApp)
            {
                app?.StopAsync().GetAwaiter().GetResult();
            }

            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private void SetDefaults()
        {
            if (ownsApp)
            {
                WebAppBuilder.WebHost.UseUrls((from t in hostAddresses.Split(';') select t.Trim()).ToArray());
                WebAppBuilder.WebHost.UseKestrel(k =>
                {
                    k.Limits.MaxRequestBodySize = null;
                });

                WebAppBuilder.Services.AddControllers();
            }
        }

        public event EventHandler Disposed;
    }
}
