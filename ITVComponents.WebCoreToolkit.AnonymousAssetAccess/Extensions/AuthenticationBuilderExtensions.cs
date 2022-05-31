using System;
using Microsoft.AspNetCore.Authentication;

namespace ITVComponents.WebCoreToolkit.AnonymousAssetAccess.Extensions
{
    public static class AuthenticationBuilderExtensions
    {
        /// <summary>
        /// Initializes ApiKey support for the Application
        /// </summary>
        /// <param name="authenticationBuilder">the authenticatinoBuilder instance</param>
        /// <param name="options">a callback configuring the apiKey handler</param>
        /// <returns>the provided authenticationbuilder instance</returns>
        public static AuthenticationBuilder AddAnonymousAssetSupport(this AuthenticationBuilder authenticationBuilder, Action<AnonymousAssetAuthenticationOptions> options)
        {
            return authenticationBuilder.AddScheme<AnonymousAssetAuthenticationOptions, AnonymousAssetAuthenticationHandler>(AnonymousAssetAuthenticationOptions.DefaultScheme, options);
        }
    }
}
