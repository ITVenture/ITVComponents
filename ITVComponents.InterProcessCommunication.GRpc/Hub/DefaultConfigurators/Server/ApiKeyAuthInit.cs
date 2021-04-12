using System;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.WebToolkitOverrides;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Server
{
    public class ApiKeyAuthInit:IAuthenticationConfigProvider, IAuthorizationConfigProvider
    {
        public ApiKeyAuthInit(IAuthInit hub)
        {
            hub.RegisterAuthenticationService(this);
            hub.RegisterAuthorizationService(this);
        }

        public string UniqueName { get; set; }

        /// <summary>
        /// Configures the services for a specific use-case
        /// </summary>
        /// <param name="services">the service-collection that is used to inject dependencies</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseSimpleUserNameMapping();
            services.AddScoped<ISecurityRepository, JsonSettingsSecurityRepository>();
            services.UseDefaultApiKeyResolver();
            services.UseRepositoryClaimsTransformation();
            services.EnableRoleBaseAuthorization(options =>
            {
                options.SupportedAuthenticationSchemes.Add(ApiKeyAuthenticationOptions.DefaultScheme);
            });
        }

        /// <summary>
        /// Configures the shared default of the Authentication builder
        /// </summary>
        /// <param name="sharedOptions"></param>
        public void ConfigureDefaults(AuthenticationOptions sharedOptions)
        {
            sharedOptions.DefaultScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        }

        /// <summary>
        /// Configures a specific authentication scheme on the provided builder
        /// </summary>
        /// <param name="authenticationBuilder">the used authentication builder</param>
        public void ConfigureAuth(AuthenticationBuilder authenticationBuilder)
        {
            authenticationBuilder.AddApiKeySupport(o =>
            {
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
