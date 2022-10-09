using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.SharedData;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Extensions;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.JWT;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.JWT.Impl;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIdConnectOptions = ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options.OpenIdConnectOptions;

namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string optionType, string path)
        {
            switch (optionType)
            {
                case "OpenId":
                    return config.GetSection<OpenIdConnectOptions>(path);
                case "Microsoft":
                    return config.GetSection<MicrosoftConnectOptions>(path);
                case "Google":
                    return config.GetSection<GoogleConnectOptions>(path);
                case "Facebook":
                    return config.GetSection<FacebookConnectOptions>(path);
                case "Bearer":
                    return config.GetSection<BearerConnectOptions>(path);
            }

            return null;
        }

        [ServiceRegistrationMethod]
        public static void ConfigureAuthOptions(IServiceCollection services,
            [WebPartConfig("OpenId")] OpenIdConnectOptions openIdConfig,
            [WebPartConfig("Microsoft")] MicrosoftConnectOptions microsoftConfig,
            [WebPartConfig("Google")] GoogleConnectOptions googleConfig,
            [WebPartConfig("Facebook")] FacebookConnectOptions facebookConfig,
            [WebPartConfig("Bearer")] BearerConnectOptions bearerConfig,
            [SharedObjectHeap]ISharedObjHeap sharedObjects)
        {
            //--openid connect
            bool configureOpenId = openIdConfig != null;
            string openIdScheme = string.IsNullOrEmpty(openIdConfig?.AuthSchemeExtension)
                ? OpenIdConnectDefaults.AuthenticationScheme
                : $"{OpenIdConnectDefaults.AuthenticationScheme}.{openIdConfig.AuthSchemeExtension}";
            //--microsoft connect
            bool configureMicrosoft = microsoftConfig != null;
            string microsoftScheme = string.IsNullOrEmpty(microsoftConfig?.AuthSchemeExtension)
                ? MicrosoftAccountDefaults.AuthenticationScheme
                : $"{MicrosoftAccountDefaults.AuthenticationScheme}.{microsoftConfig.AuthSchemeExtension}";
            //--google connect
            bool configureGoogle = googleConfig!= null;
            string googleScheme = string.IsNullOrEmpty(googleConfig?.AuthSchemeExtension)
                ? GoogleDefaults.AuthenticationScheme
                : $"{GoogleDefaults.AuthenticationScheme}.{googleConfig.AuthSchemeExtension}";
            //--facebook connect
            bool configureFacebook = facebookConfig!= null;
            string facebookScheme = string.IsNullOrEmpty(facebookConfig?.AuthSchemeExtension)
                ? FacebookDefaults.AuthenticationScheme
                : $"{FacebookDefaults.AuthenticationScheme}.{facebookConfig.AuthSchemeExtension}";
            //-- bearer connect
            bool configureBearer = bearerConfig != null;
            string bearerScheme = string.IsNullOrEmpty(bearerConfig?.AuthSchemeExtension)
                ? JwtBearerDefaults.AuthenticationScheme
                : $"{JwtBearerDefaults.AuthenticationScheme}.{bearerConfig.AuthSchemeExtension}";
            if (bearerConfig is { UseTokenGenerator: true })
            {
                var l = sharedObjects.Property<List<string>>("SignInSchemes", true);
                l.Value.AddIfMissing(bearerScheme, true);
                services.Configure<JwtGeneratorOptions>(o =>
                {
                    o.Issuer = bearerConfig.TokenValidationParameters.ValidIssuer;
                    o.Audience = bearerConfig.TokenValidationParameters.ValidAudience;
                    o.IssuerKey = bearerConfig.TokenValidationParameters.IssuerSigningKey;
                    o.TokenDuration = bearerConfig.TokenDuration;
                    o.IncludedClaims.AddRange(bearerConfig.Claims);
                    o.ValidateAudience = bearerConfig.TokenValidationParameters.ValidateAudience;
                    o.ValidateIssuer = bearerConfig.TokenValidationParameters.ValidateIssuer;
                    o.ValidateIssuerSigningKey = bearerConfig.TokenValidationParameters.ValidateIssuerSigningKey;
                    o.ValidIssuer = bearerConfig.TokenValidationParameters.ValidIssuer;
                    o.ValidAudience = bearerConfig.TokenValidationParameters.ValidAudience;
                    o.ApplicationKeyClaim = bearerConfig.ApplicationIdClaim;
                });
                services.AddScoped<IJwtService, JwtTokenService>();
                services.Configure<UserMappingOptions>(o =>
                {
                    o.MapApplicationId = bearerConfig.MapApplicationId;
                });
            }

            if (configureOpenId | configureMicrosoft | configureGoogle | configureFacebook | configureBearer)
            {
                services.Configure<AuthenticationHandlerOptions>(o =>
                {
                    if (configureOpenId)
                    {
                        o.AuthenticationHandlers.Add(new AuthenticationHandlerDefinition
                        {
                            AuthenticationSchemeName = openIdScheme,
                            DisplayInHandlerSelection = openIdConfig.Selectable??false,
                            DisplayName = openIdConfig.Name,
                            LogoFile = openIdConfig.LogoFile
                        });
                    }

                    if (configureMicrosoft)
                    {
                        o.AuthenticationHandlers.Add(new AuthenticationHandlerDefinition
                        {
                            AuthenticationSchemeName = microsoftScheme,
                            DisplayInHandlerSelection = microsoftConfig.Selectable ?? false,
                            DisplayName = microsoftConfig.Name,
                            LogoFile = microsoftConfig.LogoFile
                        });
                    }

                    if (configureGoogle)
                    {
                        o.AuthenticationHandlers.Add(new AuthenticationHandlerDefinition
                        {
                            AuthenticationSchemeName = googleScheme,
                            DisplayInHandlerSelection = googleConfig.Selectable ?? false,
                            DisplayName = googleConfig.Name,
                            LogoFile = googleConfig.LogoFile
                        });
                    }

                    if (configureFacebook)
                    {
                        o.AuthenticationHandlers.Add(new AuthenticationHandlerDefinition
                        {
                            AuthenticationSchemeName = facebookScheme,
                            DisplayInHandlerSelection = facebookConfig.Selectable ?? false,
                            DisplayName = facebookConfig.DisplayName,
                            LogoFile = facebookConfig.LogoFile
                        });
                    }

                    if (configureBearer)
                    {
                        o.AuthenticationHandlers.Add(new AuthenticationHandlerDefinition
                        {
                            AuthenticationSchemeName = bearerScheme,
                            DisplayInHandlerSelection = bearerConfig.Selectable ?? false,
                            DisplayName = bearerConfig.Name,
                            LogoFile = bearerConfig.LogoFile
                        });
                    }
                });
            }
        }

        [EndpointRegistrationMethod]
        public static void RegisterEndpoints(WebApplication app,
            [WebPartConfig("Bearer")] BearerConnectOptions bearerConfig)
        {
            if (bearerConfig != null && bearerConfig.ExposeTokenEndpoints)
            {
                app.UseTokenEndpoints();
                
            }
        }

        [AuthenticationRegistrationMethod]
        public static void RegisterAuthenticator(AuthenticationBuilder auth, 
            [WebPartConfig("OpenId")] OpenIdConnectOptions openIdConfig,
            [WebPartConfig("Microsoft")] MicrosoftConnectOptions microsoftConfig,
            [WebPartConfig("Google")] GoogleConnectOptions googleConfig,
            [WebPartConfig("Facebook")] FacebookConnectOptions facebookConfig,
            [WebPartConfig("Bearer")] BearerConnectOptions bearerConfig)
        {
            if (openIdConfig != null)
            {
                auth.OpenIdQuick(openIdConfig);
            }

            if (microsoftConfig != null)
            {
                auth.MicrosoftQuick(microsoftConfig);
            }

            if (googleConfig != null)
            {
                auth.GoogleQuick(googleConfig);
            }

            if (facebookConfig != null)
            {
                auth.FacebookQuick(facebookConfig);
            }

            if (bearerConfig != null)
            {
                auth.BearerQuick(bearerConfig);
            }
        }
    }
}
