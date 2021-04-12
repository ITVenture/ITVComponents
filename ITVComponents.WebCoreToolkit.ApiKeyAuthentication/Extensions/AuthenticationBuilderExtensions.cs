using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.WebCoreToolkit.ApiKeyAuthentication.Extensions
{
    public static class AuthenticationBuilderExtensions
    {
        /// <summary>
        /// Initializes ApiKey support for the Application
        /// </summary>
        /// <param name="authenticationBuilder">the authenticatinoBuilder instance</param>
        /// <param name="options">a callback configuring the apiKey handler</param>
        /// <returns>the provided authenticationbuilder instance</returns>
        public static AuthenticationBuilder AddApiKeySupport(this AuthenticationBuilder authenticationBuilder, Action<ApiKeyAuthenticationOptions> options)
        {
            return authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, options);
        }
    }
}
