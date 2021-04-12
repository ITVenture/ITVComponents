#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
//using Sustainsys.Saml2.Metadata.Services;
using ITVComponents.Security;
namespace ITVComponents.WebCoreToolkit.Saml.Extensions
{
    public static class Saml2Extensions
    {
        /// <summary>
        /// Gets a value indicating whether the saml-requests are secure
        /// </summary>
        public static bool SamlRequestsSigned { get; private set; }

        /// <summary>
        /// Configures the Company in ServiceProvicer - options
        /// </summary>
        /// <param name="providerOptions">the ServiceProvider options</param>
        /// <param name="name">the name of the company</param>
        /// <param name="url">the uri of the company-Webpage</param>
        /// <param name="language">the language of the company-record</param>
        /// <param name="displayName">the displayName of the company. If omitted, the name is used</param>
        /// <returns></returns>
        public static SPOptions WithDefaultCompany(this SPOptions providerOptions, string name, string url, string language, string? displayName = null)
        {
            providerOptions.Organization = new Organization();
            providerOptions.Organization.Urls.Add(new LocalizedUri(new Uri(url), language));
            providerOptions.Organization.DisplayNames.Add(new LocalizedName(displayName ?? name, language));
            providerOptions.Organization.Names.Add(new LocalizedName(name, language));
            return providerOptions;
        }

        /// <summary>
        /// Configures a Contact-Record on the ServiceProvider options
        /// </summary>
        /// <param name="providerOptions">the ServiceProvider options</param>
        /// <param name="givenName">the given name of the contact</param>
        /// <param name="company">the company of the contact</param>
        /// <param name="emailAddress">the e-mail address of the contact</param>
        /// <param name="phoneNumber">the phone number of the contact</param>
        /// <returns></returns>
        public static SPOptions WithContact(this SPOptions providerOptions, string givenName, string company, string? emailAddress, string? phoneNumber)
        {
            var contact = new ContactPerson(ContactType.Technical)
            {
                Company = company,
                GivenName = givenName,
            };
            if (!string.IsNullOrEmpty(emailAddress))
            {
                contact.EmailAddresses.Add(emailAddress);
            }

            if (!string.IsNullOrEmpty(phoneNumber))
            {
                contact.TelephoneNumbers.Add(phoneNumber);
            }

            providerOptions.Contacts.Add(contact);
            return providerOptions;
        }

        /// <summary>
        /// Configures an attribute consuming service for the given ServiceProvider options
        /// </summary>
        /// <param name="providerOptions">the serviceProvider options</param>
        /// <param name="serviceName">the servideName of the consumer-service</param>
        /// <param name="language">the language for the Service-Record</param>
        /// <param name="isDefault">indicates whether this is the default-service</param>
        /// <param name="configureService">an action that can be used to configure the service</param>
        /// <returns>the ServiceProvider options that were passed as argument</returns>
        public static SPOptions WithService(this SPOptions providerOptions, string serviceName, string language, bool isDefault, Action<AttributeConsumingService>? configureService = null)
        {
            var svc = new AttributeConsumingService();
            svc.ServiceNames.Add(new LocalizedName(serviceName, language));
            svc.IsDefault = isDefault;
            if (configureService != null){
                configureService(svc);
            }
            else
            {
                svc.RequestName().RequestEmail().RequestPersistentNameId();
            }
            providerOptions.AttributeConsumingServices.Add(svc);
            return providerOptions;
        }

        /// <summary>
        /// Configures an attribute consuming service for the given ServiceProvider options
        /// </summary>
        /// <param name="providerOptions">the serviceProvider options</param>
        /// <param name="storeName">the store from which to get the certificate</param>
        /// <param name="location">the store-location</param>
        /// <param name="serialNumber">the serial-number of the certificate</param>
        /// <returns>the ServiceProvider options that was passed as argument</returns>
        public static SPOptions WithCertificate(this SPOptions providerOptions, StoreName storeName, StoreLocation location, string serialNumber)
        {
            try
            {
                using (X509Store store = new X509Store(storeName, location))
                {
                    store.Open(OpenFlags.ReadOnly);
                    var tmp = store.Certificates.Find(X509FindType.FindBySerialNumber, serialNumber, true);
                    if (tmp.Count != 0)
                    {
                        var cert = tmp[0];
                        providerOptions.ServiceCertificates.Add(cert);
                        SamlRequestsSigned = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
            }

            return providerOptions;
        }

        /// <summary>
        /// Configures an attribute consuming service for the given ServiceProvider options
        /// </summary>
        /// <param name="providerOptions">the serviceProvider options</param>
        /// <param name="pathToPfx">the path to the pfx file that is used</param>
        /// <param name="passwordForPfx">the password for the pfx file</param>
        /// <returns>the ServiceProvider options that was passed as argument</returns>
        public static SPOptions WithCertificate(this SPOptions providerOptions, string pathToPfx, string passwordForPfx)
        {
            try
            {
                var cert = new X509Certificate2(pathToPfx, passwordForPfx.Secure(), X509KeyStorageFlags.PersistKeySet);
                providerOptions.ServiceCertificates.Add(cert);
                SamlRequestsSigned = true;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
            }

            return providerOptions;
        }

        /// <summary>
        /// Requests a specific claim
        /// </summary>
        /// <param name="service">the service on which to request a specific claim</param>
        /// <param name="attribute">the target claim to request</param>
        /// <returns>the attribute-Service that was passed as argument</returns>
        public static AttributeConsumingService RequestAttribute(this AttributeConsumingService service, RequestedAttribute attribute)
        {
            service.RequestedAttributes.Add(attribute);
            return service;
        }

        /// <summary>
        /// Requests a specific claim
        /// </summary>
        /// <param name="service">the service on which to request the Name-Claim</param>
        /// <returns>the attribute-Service that was passed as argument</returns>
        public static AttributeConsumingService RequestName(this AttributeConsumingService service)
        {
            return service.RequestAttribute(new RequestedAttribute("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
            {
                FriendlyName = "Name",
                IsRequired = false,
                NameFormat = new Uri("urn:oasis:names:tc:SAML:2.0:attrname-format:uri")
            });
        }

        /// <summary>
        /// Requests a specific claim
        /// </summary>
        /// <param name="service">the service on which to request the E-Mail address claim</param>
        /// <returns>the attribute-Service that was passed as argument</returns>
        public static AttributeConsumingService RequestEmail(this AttributeConsumingService service)
        {
            return service.RequestAttribute(new RequestedAttribute("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
            {
                FriendlyName="E-Mail-Addresses",
                IsRequired=false,
                NameFormat=new Uri("urn:oasis:names:tc:SAML:2.0:attrname-format:uri")
            });
        }

        /// <summary>
        /// Requests a specific claim
        /// </summary>
        /// <param name="service">the service on which to request the NameId - Claim</param>
        /// <returns>the attribute-Service that was passed as argument</returns>
        public static AttributeConsumingService RequestPersistentNameId(this AttributeConsumingService service)
        {
            return service.RequestAttribute(new RequestedAttribute("nameid:persistent")
            {
                FriendlyName = "mail",
                IsRequired = false,
                NameFormat = new Uri("urn:oasis:names:tc:SAML:2.0:attrname-format:uri")
            });
        }
    }
}
