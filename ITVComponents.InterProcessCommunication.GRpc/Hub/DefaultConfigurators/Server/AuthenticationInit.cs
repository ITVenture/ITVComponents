using System;
using System.Collections.Generic;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Server
{
    public class AuthenticationInit:IAppConfigureProvider, IServicesConfigureProvider, IAuthInit
    {
        private List<IAuthorizationConfigProvider> authorizationProviders = new List<IAuthorizationConfigProvider>();

        private List<IAuthenticationConfigProvider> authenticationProviders = new List<IAuthenticationConfigProvider>();

        /// <summary>
        /// Initializes a new instance of the AuthenticationInit class
        /// </summary>
        /// <param name="parent">the object that initializes the web-host</param>
        public AuthenticationInit(IServiceHubProvider parent)
        {
            parent.RegisterAppConfigureProvider(this);
            parent.RegisterServicesConfigureProvider(this);
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
            if (authenticationProviders.Count != 0)
            {
                var authenticationBuilder = services.AddAuthentication(sharedOptions =>
                {
                    authenticationProviders.ForEach(n => n.ConfigureDefaults(sharedOptions));
                });

                authenticationProviders.ForEach(n => n.ConfigureAuth(authenticationBuilder));
            }

            if (authorizationProviders.Count != 0)
            {
                services.AddAuthorization();
                authorizationProviders.ForEach(n => n.ConfigureServices(services));
            }
        }

        /// <summary>
        /// Configures the app-builder / host-environment before the services are configured
        /// </summary>
        /// <param name="app">the app-builder</param>
        /// <param name="env">the hosting environment</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            if (authenticationProviders.Count != 0)
            {
                app.UseAuthentication();
            }

            if (authorizationProviders.Count != 0)
            {
                app.UseAuthorization();
            }
        }

        /// <summary>
        /// Registers an authorization service on this init-instance
        /// </summary>
        /// <param name="provider">the config-provider that will inject configs at the appropriate point in time</param>
        public void RegisterAuthorizationService(IAuthorizationConfigProvider provider)
        {
            authorizationProviders.Add(provider);
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
