using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using ITVComponents.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Server
{
    public class SslHubInit:IAppConfigureProvider, IServicesConfigureProvider,IEndPointDefaultsConfigurator
    {
        private readonly IServiceHubProvider parent;
        private readonly bool withHsts;
        private readonly int httpsRedirectPort;
        private readonly string pathToCertificate;
        private readonly string certificatePassword;
        private readonly string certificateSerial;
        private readonly bool certificateAutoSelect;


        /// <summary>
        /// Initializes a new instance of the AuthenticationInit class
        /// </summary>
        /// <param name="parent">the object that initializes the web-host</param>
        public SslHubInit(IServiceHubProvider parent, bool withHsts, int httpsRedirectPort, string pathToCertificate, string certificatePassword)
        {
            this.parent = parent;
            this.withHsts = withHsts;
            this.httpsRedirectPort = httpsRedirectPort;
            this.pathToCertificate = pathToCertificate;
            this.certificatePassword = certificatePassword;
            parent.RegisterAppConfigureProvider(this);
            parent.RegisterServicesConfigureProvider(this);
            parent.RegisterEndPointDefaultsConfigurator(this);
        }

        /// <summary>
        /// Initializes a new instance of the AuthenticationInit class
        /// </summary>
        /// <param name="parent">the object that initializes the web-host</param>
        public SslHubInit(IServiceHubProvider parent, bool withHsts, int httpsRedirectPort)
        {
            this.parent = parent;
            this.withHsts = withHsts;
            this.httpsRedirectPort = httpsRedirectPort;
            parent.RegisterAppConfigureProvider(this);
            parent.RegisterServicesConfigureProvider(this);
            parent.RegisterEndPointDefaultsConfigurator(this);
            certificateAutoSelect = true;
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Configures the services for a specific use-case
        /// </summary>
        /// <param name="services">the service-collection that is used to inject dependencies</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpsRedirection(co =>
            {
                if (httpsRedirectPort > 80)
                {
                    co.HttpsPort = httpsRedirectPort;
                    co.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                }
            });
        }

        /// <summary>
        /// Configures the app-builder / host-environment before the services are configured
        /// </summary>
        /// <param name="app">the app-builder</param>
        /// <param name="env">the hosting environment</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (withHsts)
            {
                app.UseHsts();
            }
        }

        /// <summary>
        /// Configures the ListenOptions for the Kestrel-Server
        /// </summary>
        /// <param name="options">the default-endpoint-listener options</param>
        public void ConfigureEndPointDefaults(ListenOptions options)
        {
            options.UseHttps(ho =>
            {
                if (certificateAutoSelect)
                {
                    ho.ServerCertificateSelector = (context, name) =>
                    {
                        return CertificateLoader.LoadFromStoreCert(name, StoreName.My.ToString(), StoreLocation.LocalMachine, false);
                    };
                }
                else
                {
                    ho.ServerCertificate = new X509Certificate2(pathToCertificate, certificatePassword.Decrypt().Secure(), X509KeyStorageFlags.PersistKeySet);
                }
            });
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            OnDisposed();
        }

        /// <summary>
        /// Raises the Disposed event
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Informs a calling class of a Disposal of this Instance
        /// </summary>
        public event EventHandler Disposed;
    }
}
