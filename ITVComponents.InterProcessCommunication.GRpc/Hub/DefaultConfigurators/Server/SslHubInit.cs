using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using ITVComponents.Plugins;
using ITVComponents.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Server
{
    public class SslHubInit:IServiceHubConfigurator, IPlugin
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
            parent.RegisterConfigurator(this);
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
            certificateAutoSelect = true;
            parent.RegisterConfigurator(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Configures the WebApplication builder (inject services, set defaults, etc.)
        /// </summary>
        /// <param name="builder">the web-application builder that is used to setup a grpc service</param>
        public void ConfigureBuilder(WebApplicationBuilder builder)
        {
            builder.Services.AddHttpsRedirection(co =>
            {
                if (httpsRedirectPort > 80)
                {
                    co.HttpsPort = httpsRedirectPort;
                    co.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                }
            });

            builder.WebHost.ConfigureKestrel(ko =>
            {
                ko.ConfigureEndpointDefaults(li => li.UseHttps(ho =>
                {
                    if (certificateAutoSelect)
                    {
                        ho.ServerCertificateSelector = (context, name) =>
                        {
                            return CertificateLoader.LoadFromStoreCert(name, StoreName.My.ToString(),
                                StoreLocation.LocalMachine, false);
                        };
                    }
                    else
                    {
                        ho.ServerCertificate = new X509Certificate2(pathToCertificate,
                            certificatePassword.Decrypt().Secure(), X509KeyStorageFlags.PersistKeySet);
                    }
                }));
            });
        }

        /// <summary>
        /// Configures the app after it is built. (e.g. build the service middleware pipeline
        /// </summary>
        /// <param name="app">the built app</param>
        public void ConfigureApp(WebApplication app)
        {
            if (withHsts)
            {
                app.UseHsts();
            }
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
