using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamitey;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIdConnectOptions = ITVComponents.WebCoreToolkit.OpenIdAuthentication.Options.OpenIdConnectOptions;
using OidOpt = Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions;
using TokenValidatedContext = Microsoft.AspNetCore.Authentication.JwtBearer.TokenValidatedContext;

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
                o.AccessDeniedPath = options.AccessDeniedPath;
                if (options.Scope != null)
                {
                    options.Scope.ForEach(o.Scope.Add);
                }
            };

            string scheme = string.IsNullOrEmpty(options.AuthSchemeExtension)
                ? MicrosoftAccountDefaults.AuthenticationScheme
                : $"{MicrosoftAccountDefaults.AuthenticationScheme}.{options.AuthSchemeExtension}";
            if (string.IsNullOrEmpty(options.Name))
            {
                return auth.AddMicrosoftAccount(scheme, c);
            }

            return auth.AddMicrosoftAccount(scheme, options.Name, c);
        }

        public static AuthenticationBuilder OpenIdQuick(this AuthenticationBuilder auth, OpenIdConnectOptions options)
        {
            Action<OidOpt> c = o =>
            {
                var shrinkDic = new ConcurrentDictionary<string, Dictionary<string, string?>>();
                var rnd = new Random();
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
                o.AccessDeniedPath = options.AccessDeniedPath;
                //o.StateDataFormat = new Horn();
                //o.StateDataFormat = new SecureDataFormat<AuthenticationProperties>()
                if (!string.IsNullOrEmpty(options.AuthSchemeExtension))
                {
                    o.CallbackPath = $"{o.CallbackPath}-{options.AuthSchemeExtension}";
                    o.SignedOutCallbackPath= $"{o.SignedOutCallbackPath}-{options.AuthSchemeExtension}";
                }

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
                    return Task.CompletedTask;
                };

                if (options.ShrinkStatus)
                {
                    o.Events.OnMessageReceived = context =>
                    {
                        if (context.Properties.Items.ContainsKey("IT-BUF"))
                        {
                            var key = context.Properties.Items["IT-BUF"];
                            if (shrinkDic.TryRemove(key, out var data))
                            {
                                context.Properties.Items.Remove("IT-BUF");
                                foreach (var item in data)
                                {
                                    context.Properties.Items.Add(item.Key, item.Value);
                                }
                            }
                        }

                        return Task.CompletedTask;
                    };

                    o.Events.OnRedirectToIdentityProvider = context =>
                    {
                        var dic = new Dictionary<string, string?>(context.Properties.Items);
                        var key = $"{rnd.Next(65535)}-{DateTime.Now.Ticks}";
                        while (!shrinkDic.TryAdd(key, dic))
                        {
                            key = $"{rnd.Next(65535)}-{DateTime.Now.Ticks}";
                        }

                        context.Properties.Items.Clear();
                        context.Properties.Items.Add("IT-BUF",key);
                        return Task.CompletedTask;
                    };
                }

                o.Events.OnAuthenticationFailed = context =>
                {
                    LogEnvironment.LogEvent(context.Exception.OutlineException(), LogSeverity.Error);
                    return Task.CompletedTask;
                };
            };
            
            string scheme = string.IsNullOrEmpty(options.AuthSchemeExtension)
                ? OpenIdConnectDefaults.AuthenticationScheme
                : $"{OpenIdConnectDefaults.AuthenticationScheme}.{options.AuthSchemeExtension}";
            if (string.IsNullOrEmpty(options.Name))
            {
                return auth.AddOpenIdConnect(scheme, c);

            }

            return auth.AddOpenIdConnect(
                scheme, options.Name, c);
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
                o.AccessDeniedPath = options.AccessDeniedPath;
                if (options.Scope != null)
                {
                    options.Scope.ForEach(o.Scope.Add);
                }
            };

            string scheme = string.IsNullOrEmpty(options.AuthSchemeExtension)
                ? GoogleDefaults.AuthenticationScheme
                : $"{GoogleDefaults.AuthenticationScheme}.{options.AuthSchemeExtension}";

            if (string.IsNullOrEmpty(options.Name))
            {
                return auth.AddGoogle(scheme, c);
            }

            return auth.AddGoogle(scheme, options.Name, c);
        }

        public static AuthenticationBuilder BearerQuick(this AuthenticationBuilder builder, BearerConnectOptions options)
        {
            var callback = (JwtBearerOptions op) =>
            {
                op.Authority = options.Authority;
                op.SaveToken = options.SaveToken;
                op.RequireHttpsMetadata = options.RequireHttpsMetadata;
                JwtBearerEvents jbe = null;
                if (options.TokenValidationParameters != null)
                {
                    op.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = options.TokenValidationParameters.ValidateIssuer,
                        ValidateAudience = options.TokenValidationParameters.ValidateAudience,
                        ValidateLifetime = options.TokenValidationParameters.ValidateLifetime,
                        ValidateIssuerSigningKey = options.TokenValidationParameters.ValidateIssuerSigningKey,
                        ClockSkew = TimeSpan.FromSeconds(options.TokenValidationParameters.ClockSkew),
                        ValidAudience = options.TokenValidationParameters.ValidAudience,
                        ValidIssuer = options.TokenValidationParameters.ValidIssuer,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.TokenValidationParameters.IssuerSigningKey))
                    }; //options.TokenValidationParameters;

                    if (!string.IsNullOrEmpty(options.EventHandler))
                    {
                        jbe = (JwtBearerEvents)ExpressionParser.Parse(options.EventHandler,
                            new Dictionary<string, object>());
                    }
                }

                if (jbe != null)
                {
                    op.Events = jbe; //new JwtBearerEvents().;
                }

                /*{
                    OnTokenValidated = context =>
                    {
                        Console.WriteLine("hallo");
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = async context =>
                    {
                        Console.WriteLine("Fail!");
                    },
                    OnMessageReceived = async context =>
                    {
                        Console.WriteLine("MessageHere");
                    },
                    OnChallenge = async context =>
                    {
                        Console.WriteLine("Challenged!");
                    },
                    OnForbidden = async context =>
                    {
                        Console.WriteLine("Forbid!");
                    }
                };*/
            };

            string scheme = string.IsNullOrEmpty(options.AuthSchemeExtension)
                ? JwtBearerDefaults.AuthenticationScheme
                : $"{JwtBearerDefaults.AuthenticationScheme}.{options.AuthSchemeExtension}";
            if (!string.IsNullOrEmpty(options.Name))
            {
                return builder.AddJwtBearer(scheme, options.Name,
                    callback);
            }
            else
            {
                return builder.AddJwtBearer(scheme,
                    callback);
            }
        }

        public static AuthenticationBuilder FacebookQuick(this AuthenticationBuilder builder,
            FacebookConnectOptions options)
        {
            Action<FacebookOptions> configureOptions = cfg =>
            {
                cfg.AppId = options.AppId;
                cfg.AppSecret = options.AppSecret;
                cfg.AccessDeniedPath = options.AccessDeniedPath;
            };
            string scheme = string.IsNullOrEmpty(options.AuthSchemeExtension)
                ? FacebookDefaults.AuthenticationScheme
                : $"{FacebookDefaults.AuthenticationScheme}.{options.AuthSchemeExtension}";
            if (string.IsNullOrEmpty(options.DisplayName))
            {
                return builder.AddFacebook(scheme, configureOptions);
            }

            return builder.AddFacebook(scheme, options.DisplayName, configureOptions);
        }
    }
}
