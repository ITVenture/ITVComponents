using System;
using System.Collections.Generic;
using ITVComponents.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Configurators.DefaultImpl
{
    public class AuthenticationInit:IServiceHostConfigurator, IAuthInit, IPlugin
    {
        private readonly bool useAuthorization;
        private List<IAuthenticationConfigProvider> authenticationProviders = new List<IAuthenticationConfigProvider>();

        /// <summary>
        /// Initializes a new instance of the AuthenticationInit class
        /// </summary>
        /// <param name="parent">the object that initializes the web-host</param>
        public AuthenticationInit(IWebHostStartup parent, bool useAuthorization)
        {
            this.useAuthorization = useAuthorization;
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
            if (authenticationProviders.Count != 0)
            {
                var authenticationBuilder = builder.Services.AddAuthentication(sharedOptions =>
                {
                    authenticationProviders.ForEach(n => n.ConfigureDefaults(sharedOptions));
                });

                authenticationProviders.ForEach(n => n.ConfigureAuth(authenticationBuilder));
            }

            if (useAuthorization)
            {
                builder.Services.AddAuthorization();
            }
        }

        /// <summary>
        /// Configures the app after it is built. (e.g. build the service middleware pipeline
        /// </summary>
        /// <param name="app">the built app</param>
        public void ConfigureApp(WebApplication app)
        {
            if (authenticationProviders.Count != 0)
            {
                app.UseAuthentication();
            }

            if (useAuthorization)
            {
                app.UseAuthorization();
            }
        }

        /// <summary>
        /// Registers an authentication service on this init instance
        /// </summary>
        /// <param name="provider">the config-provider that will inject configs at the appropriate point in time</param>
        public void RegisterAuthenticationService(IAuthenticationConfigProvider provider)
        {
            authenticationProviders.Add(provider);
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
