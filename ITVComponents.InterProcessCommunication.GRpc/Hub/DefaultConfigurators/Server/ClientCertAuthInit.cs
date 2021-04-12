using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.DefaultConfigurators.Server
{
    public class ClientCertAuthInit:IAuthenticationConfigProvider
    {
        /// <summary>
        /// Initializes a new instance of the ClientCertAuthInit class
        /// </summary>
        /// <param name="parent">the object that initializes the auth-services on a web-host</param>
        public ClientCertAuthInit(IAuthInit parent)
        {
            parent.RegisterAuthenticationService(this);
        }

        /// <summary>
        /// Gets or sets the UniqueName of this Plugin
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Configures the shared default of the Authentication builder
        /// </summary>
        /// <param name="sharedOptions"></param>
        public void ConfigureDefaults(AuthenticationOptions sharedOptions)
        {
        }

        /// <summary>
        /// Configures a specific authentication scheme on the provided builder
        /// </summary>
        /// <param name="authenticationBuilder">the used authentication builder</param>
        public void ConfigureAuth(AuthenticationBuilder authenticationBuilder)
        {
            authenticationBuilder.AddCertificate(
                opt =>
                {
                    opt.AllowedCertificateTypes = CertificateTypes.All;
                    if (!HubConfiguration.Helper.TrustAllCertificates)
                    {
                        opt.Events.OnCertificateValidated = context =>
                        {
                            if (!HubConfiguration.Helper.TrustedCertificates.Any(n => n.Equals(context.ClientCertificate.SerialNumber, StringComparison.OrdinalIgnoreCase)))
                            {
                                context.Fail($"The Certificate with the SerialNumber {context.ClientCertificate.SerialNumber} is untrusted!");
                            }

                            return Task.CompletedTask;
                        };
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
