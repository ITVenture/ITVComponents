using System;
using ITVComponents.GenericService.ServiceSecurity;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication;
using ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Extensions;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.GenericService.WebService.Configurators.DefaultImpl
{
    public class ApiKeyAuthInit:IAuthenticationConfigProvider, IServiceHostConfigurator
    {
        public ApiKeyAuthInit(IWebHostStartup hubProvider,IAuthInit hub)
        {
            hub.RegisterAuthenticationService(this);
            hubProvider.RegisterConfigurator(this);
        }

        public string UniqueName { get; set; }

        /// <summary>
        /// Configures the WebApplication builder (inject services, set defaults, etc.)
        /// </summary>
        /// <param name="builder">the web-application builder that is used to setup a grpc service</param>
        public void ConfigureBuilder(WebApplicationBuilder builder)
        {
            builder.Services.UseSimpleUserNameMapping()
            .AddScoped<ISecurityRepository, JsonSettingsSecurityRepository>()
            .UseDefaultApiKeyResolver()
            .UseRepositoryClaimsTransformation()
            .EnableRoleBaseAuthorization();
        }

        /// <summary>
        /// Configures the app after it is built. (e.g. build the service middleware pipeline
        /// </summary>
        /// <param name="app">the built app</param>
        public void ConfigureApp(WebApplication app)
        {
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
