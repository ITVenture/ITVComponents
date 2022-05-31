using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using OpenIdConnectOptions = ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options.OpenIdConnectOptions;
using OidOpt = Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions;
namespace ITVComponents.WebCoreToolkit.OpenIdAuthentication.Extensions
{
    public static class AuthenticationBuilderExtensions
    {
        public static AuthenticationBuilder MicrosoftQuick(this AuthenticationBuilder auth,
            MicrosoftConnectOptions options)
        {
            Action<MicrosoftAccountOptions> c = o =>
            {
                o.SignInScheme = options.SignInScheme;
                o.ClientId = options.ClientId;
                o.ClientSecret = options.ClientSecret;
                o.SaveTokens = options.SaveTokens;
                if (options.Scope != null)
                {
                    options.Scope.ForEach(o.Scope.Add);
                }
            };
            if (string.IsNullOrEmpty(options.Name))
            {
                return auth.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, c);
            }

            return auth.AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options.Name, c);
        }

        public static AuthenticationBuilder OpenIdQuick(this AuthenticationBuilder auth, OpenIdConnectOptions options)
        {
            Action<OidOpt> c = o =>
            {
                o.SignInScheme = options.SignInScheme;
                o.MetadataAddress = options.MetadataAddress;
                o.SignedOutRedirectUri = options.SignedOutRedirectUri;
                o.ClientId = options.ClientId;
                o.ClientSecret = options.ClientSecret;
                o.ResponseType = options.ResponseType;
                o.SaveTokens = options.SaveTokens;
                o.GetClaimsFromUserInfoEndpoint = options.GetClaimsFromUserInfoEndpoint;
                o.RequireHttpsMetadata = options.RequireHttpsMetadata;
                o.UseTokenLifetime = true;
#if NET5_0_OR_GREATER
                o.AutomaticRefreshInterval = TimeSpan.FromMinutes(20);
#endif
                if (options.Scope != null)
                {
                    options.Scope.ForEach(o.Scope.Add);
                }

                if (options.TokenToClaims)
                {
                    o.Events.OnTokenValidated = x =>
                    {
                        x.TokenToClaims();
                        return Task.CompletedTask;
                    };
                }

                o.Events.OnUserInformationReceived = context =>
                {
                    Console.WriteLine(context.User.ToString());
                    return Task.CompletedTask;
                };

                o.Events.OnMessageReceived = context =>
                {
                    Console.WriteLine(context.ProtocolMessage.Error);
                    Console.WriteLine(context.ProtocolMessage.ErrorDescription);
                    Console.WriteLine(context.ProtocolMessage.ErrorUri);
                    return Task.CompletedTask;
                };
                o.Events.OnAuthenticationFailed = context =>
                {
                    Console.WriteLine(context.Exception.OutlineException());
                    return Task.CompletedTask;
                };
            };
            if (string.IsNullOrEmpty(options.Name))
            {
                return auth.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, c);

            }
            
            return auth.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options.Name, c);
        }

        public static AuthenticationBuilder GoogleQuick(this AuthenticationBuilder auth, GoogleConnectOptions options)
        {
            Action<GoogleOptions> c = o =>
            {
                o.SignInScheme = options.SignInScheme;
                o.ClientId = options.ClientId;
                o.ClientSecret = options.ClientSecret;
                o.SaveTokens = options.SaveTokens;
                o.AccessType = options.AccessType;
                if (options.Scope != null)
                {
                    options.Scope.ForEach(o.Scope.Add);
                }
            };

            if (string.IsNullOrEmpty(options.Name))
            {
                return auth.AddGoogle(OpenIdConnectDefaults.AuthenticationScheme, c);

            }

            return auth.AddGoogle(OpenIdConnectDefaults.AuthenticationScheme, options.Name, c);
        }
    }
}
