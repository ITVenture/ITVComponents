﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.Extensions.DependencyInjection;

//using Microsoft.AspNetCore.Authentication.Certificate;

namespace ITVComponents.GenericService.WebService.Configurators.DefaultImpl
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
                    if (!WebHostConfiguration.Helper.TrustAllCertificates)
                    {
                        opt.Events.OnCertificateValidated = context =>
                        {
                            if (!WebHostConfiguration.Helper.TrustedCertificates.Any(n => n.Equals(context.ClientCertificate.SerialNumber, StringComparison.OrdinalIgnoreCase)))
                            {
                                context.Fail($"The Certificate with the SerialNumber {context.ClientCertificate.SerialNumber} is untrusted!");
                            }

                            return Task.CompletedTask;
                        };
                    }

                });
        }
    }
}
