using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Saml.Configuration;
using ITVComponents.WebCoreToolkit.Saml.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sustainsys.Saml2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.Saml2P;
using RequestedAttribute = Sustainsys.Saml2.Metadata.RequestedAttribute;

namespace ITVComponents.WebCoreToolkit.Saml
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
        {
            var retVal=  config.GetSection<SamlOptions>(path);
            config.RefResolve(retVal);
            return retVal;
        }

        [AuthenticationRegistrationMethod]
        public static void ConfigureSamlAuthentication(AuthenticationBuilder builder, SamlOptions webPartOptions)
        {
            builder.AddSaml2(options =>
            {
                options.SPOptions.EntityId = new EntityId(webPartOptions.EntityId);
                var org = options.SPOptions.WithDefaultCompany(webPartOptions.DefaultCompany.Name, webPartOptions.DefaultCompany.Url, webPartOptions.DefaultCompany.Language, webPartOptions.DefaultCompany.DisplayName);
                foreach (var c in webPartOptions.Contacts)
                {
                    org.WithContact(c.Name, c.Company, c.EmailAddress, c.PhoneNumber);
                }

                foreach (var service in webPartOptions.Services)
                {
                    org.WithService(service.Name, service.Name, service.IsDefault,
                        c =>
                        {
                            foreach (var attribute in service.RequestedAttributes)
                            {
                                c.RequestedAttributes.Add(new RequestedAttribute(attribute.Name)
                                {
                                    FriendlyName=attribute.FriendlyName,
                                    IsRequired =attribute.IsRequired,
                                    NameFormat=new Uri(attribute.NameFormat)
                                });
                            }
                        });
                }

                if (webPartOptions.Compatibility != null)
                {
                    options.SPOptions.Compatibility = new Compatibility
                    {
                        AcceptUnsignedLogoutResponses = webPartOptions.Compatibility.AcceptUnsignedLogoutResponses,
                        StrictOwinAuthenticationMode = webPartOptions.Compatibility.StrictOwinAuthenticationMode,
                        UnpackEntitiesDescriptorInIdentityProviderMetadata = webPartOptions.Compatibility.UnpackEntitiesDescriptorInIdentityProviderMetadata
                    };
                }

                if (webPartOptions.RequestsSigned)
                {
                    if (webPartOptions.SignCertificates.Count != 0)
                    {
                        foreach (var cert in webPartOptions.SignCertificates)
                        {
                            if (File.Exists(cert.FilePath))
                            {
                                org.WithCertificate(cert.FilePath,
                                    cert.Password);
                            }
                        }

                        options.SPOptions.AuthenticateRequestSigningBehavior = Enum.Parse<SigningBehavior>(webPartOptions.AuthenticateRequestSigningBehavior);
                    }
                }

                options.SPOptions.NameIdPolicy = new Saml2NameIdPolicy(null, Enum.Parse<NameIdFormat>(webPartOptions.NameIdPolicy));
                options.SPOptions.WantAssertionsSigned = webPartOptions.WantAssertionsSigned;
                foreach (var identityProvider in webPartOptions.IdentityProviders)
                {
                    options.IdentityProviders.Add(
                        new IdentityProvider(new EntityId(identityProvider.EntityId),
                            options.SPOptions)
                        {
                            LoadMetadata = identityProvider.LoadMetadata,
                            MetadataLocation = identityProvider.MetadataLocation,
                            //WantAuthnRequestsSigned = false,
                        });
                }
            });
        }
    }
}
