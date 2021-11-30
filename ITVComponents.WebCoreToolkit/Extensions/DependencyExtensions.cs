﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Configuration.Impl;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Routing.Impl;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ClaimsTransformation;
using ITVComponents.WebCoreToolkit.Security.PermissionHandling;
using ITVComponents.WebCoreToolkit.Security.UserMappers;
using ITVComponents.WebCoreToolkit.Security.UserScopes;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.Initialization;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Uses the simple UserName mapper
        /// </summary>
        /// <param name="services">the services where the mapper is injected</param>
        public static IServiceCollection UseSimpleUserNameMapping(this IServiceCollection services)
        {
            return services.AddScoped<IUserNameMapper, SimpleUserNameMapper>();
        }

        /// <summary>
        /// Initializes the PluginSystem and initializes plugins that have set the StartupRegistrationConstructor property
        /// </summary>
        /// <param name="services">the serviceCollection that is used for DependencyInjection</param>
        /// <param name="useInitPlugins">indicates whether to use the Init-Constructor of the plugins to perform startup tasks</param>
        public static IServiceCollection  InitializePluginSystem(this IServiceCollection services, bool useInitPlugins = false)
        {
            if (useInitPlugins)
            {
                services.AddSingleton<IPluginsInitOptions, GlobalPluginsInitOptions>().ConfigureOptions<UsePluginsInit>();
            }
            else
            {
                services.ConfigureOptions<NoPluginsInit>();
            }

            return services.AddHttpContextAccessor().AddScoped<IWebPluginHelper, WebPluginHelper>();
        }

        /// <summary>
        /// Configures the PluginFactory for additional services that must be made available
        /// </summary>
        /// <param name="services">the serviceCollection that is used for DependencyInjection</param>
        /// <param name="options">a method that enables the caller to configure the factory</param>
        public static IServiceCollection ConfigurePluginFactory(this IServiceCollection services, Action<FactoryOptions> options)
        {
            return services.Configure<FactoryOptions>(options);
        }

        /// <summary>
        /// Enables RoleBased Authorization for the current Web application
        /// </summary>
        /// <param name="services">the service collection for which to enable authorization</param>
        /// <param name="configuration">the IConfiguration object giving access to the application settings file</param>
        /// <param name="options">Clonfigures the options for the role-based Authorization handler</param>
        public static IServiceCollection EnableRoleBaseAuthorization(this IServiceCollection services, Action<PermissionPolicyOptions> options)
        {
            return services.AddHttpContextAccessor()
                .AddSingleton<IAuthorizationPolicyProvider, AssignedPermissionsPolicyProvider>()
                .AddScoped<IAuthorizationHandler, AssignedPermissionsHandler>()
                .Configure(options);
        }

        /// <summary>
        /// Enables objects to get services injected that are loaded using a pluginFactory
        /// </summary>
        /// <param name="services">the servicesCollection in which the JnjectedPlugins are injected</param>
        /// <param name="options">the options used to configure custom plugin-loads</param>
        public static IServiceCollection UseInjectablePlugins(this IServiceCollection services, Action<InjectablePluginOptions> options)
        {
            return services.Configure(options)
                .AddScoped(typeof(IInjectablePlugin<>), typeof(InjectablePluginImpl<>));
        }

        /// <summary>
        /// Initializes claim-driven PermissionScopes
        /// </summary>
        /// <param name="services">the DI Environment</param>
        /// <param name="options">options for the Scope-determination</param>
        public static IServiceCollection UseCookiePermissionScope(this IServiceCollection services, Action<CookieScopeOptions> options)
        {
            return services.Configure(options)
                .AddScoped<IPermissionScope, CookiePermissionScope>();
        }

        /// <summary>
        /// Uses the Claim-driven User to Groups mapper
        /// </summary>
        /// <param name="services">the services where the mapper is injected</param>
        public static IServiceCollection UseUser2GroupMapper(this IServiceCollection services)
        {
            return UseUser2GroupMapper(services, o => { });
        }

        /// <summary>
        /// Uses the Claim-driven User to Groups mapper
        /// </summary>
        /// <param name="services">the services where the mapper is injected</param>
        /// <param name="options">The options for the User2Group mapper</param>
        public static IServiceCollection UseUser2GroupMapper(this IServiceCollection services, Action<User2GroupsMappingOptions> options)
        {
            return services.Configure(options)
                .AddScoped<IUserNameMapper, User2GroupsMapper>();
        }

        /// <summary>
        /// Initializes a the usage of multiple Claims-transofmration-mechanisms
        /// </summary>
        /// <param name="services">the service where to inject the collected claims transformation instance</param>
        public static IServiceCollection UseCollectedClaimsTransformation(this IServiceCollection services)
        {
            return services.AddScoped<IClaimsTransformation, CollectedClaimsTransform>();
        }

        /// <summary>
        /// Uses Repository to add required claims to the logged-in identity
        /// </summary>
        /// <param name="services">the services where transformation-provider is injected</param>
        /// <param name="collectable">indicates whether to inject the RepositoryClaims transformer in a way, that enables the usage of multiple transformers</param>
        public static IServiceCollection UseRepositoryClaimsTransformation(this IServiceCollection services, bool collectable = false)
        {
            if (!collectable)
            {
                return services.AddScoped<IClaimsTransformation, RepositoryClaimsTransformation>();
            }

            return services.AddScoped<ICollectedClaimsProvider, RepositoryClaimsTransformation>();
        }

        /// <summary>
        /// Enables the automatic SiteNavigation builder
        /// </summary>
        /// <param name="services">the services where the navigator is injected to</param>
        /// <returns>the servicesCollection that was passed as parameter</returns>
        public static IServiceCollection UseNavigator(this IServiceCollection services)
        {
            return services.AddScoped<INavigator, Navigator>();
        }

        /// <summary>
        /// Activate Permission-Scope-driven Settings
        /// </summary>
        /// <param name="services">the Services-collection where to inject the Scope-Settings-builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseScopedSettings(this IServiceCollection services)
        {
            return services.AddScoped(typeof(IScopedSettings<>), typeof(ScopedSettingsImpl<>));
        }

        /// <summary>
        /// Activate Global Settings
        /// </summary>
        /// <param name="services">the Services-collection where to inject the Global-Settings-builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseGlobalSettings(this IServiceCollection services)
        {
            return services.AddScoped(typeof(IGlobalSettings<>), typeof(GlobalSettingsImpl<>));
        }

        /// <summary>
        /// Activate Hierarchy Settings. This means, that Scoped Settings are provided, if available, and otherwise global
        /// </summary>
        /// <param name="services">the Services-collection where to inject the Hierarchy-Settings-builder instance</param>
        /// <returns>the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseHierarchySettings(this IServiceCollection services)
        {
            return services.AddScoped(typeof(IHierarchySettings<>), typeof(HierarchySettingsImpl<>));
        }

        /// <summary>
        /// Configures Localization bindings for the current application. Use this, if you intend to mapp specific Cultures to a different culture
        /// </summary>
        /// <param name="services">the service-collection where ot inject the settings</param>
        /// <param name="options">the options-configurator</param>
        /// <returns>a the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection ConfigureLocalization(this IServiceCollection services, Action<CultureOptions> options)
        {
            return services.Configure(options).AddLocalization(l =>
            {
                l.ResourcesPath = "Resources";
            });
        }
        
        /// <summary>
        /// Enables the Toolkit default validation messages for the current application
        /// </summary>
        /// <param name="services">the service-collection where to inject the Toolkit-message resolver</param>
        /// <returns>the servicecollection instance that was passed as argument</returns>
        public static IServiceCollection UseToolkitMvcMessages(this IServiceCollection services)
        {
            return services.AddSingleton<IConfigureOptions<MvcOptions>, MvcModelMessageLocalizer>();
        }
        
        /// <summary>
        /// Configures Localization bindings for the current application. Use this, if you intend to mapp specific Cultures to a different culture
        /// </summary>
        /// <param name="services">the service-collection where ot inject the settings</param>
        /// <returns>a the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection UseUrlFormatter(this IServiceCollection services)
        {
            return services.AddScoped<IUrlFormat,UrlFormatImpl>();
        }

        public static AttributeTranslationOptions ConfigureAttributeTranslation(this IServiceCollection services)
        {
            return ConfigureAttributeTranslation(services, o => { });
        }

        public static AttributeTranslationOptions ConfigureAttributeTranslation(this IServiceCollection services, Action<AttributeTranslationOptions> options)
        {
            var o = new AttributeTranslationOptions();
            o.MapResource("ITV", typeof(DefaultModelMessages));
            o.MapAttribute(typeof(CustomValidationAttribute),"ITV");
            o.MapAttribute(typeof(MaxLengthAttribute),"ITV");
            o.MapAttribute(typeof(MinLengthAttribute),"ITV");
            o.MapAttribute(typeof(RangeAttribute),"ITV");
            o.MapAttribute(typeof(RegularExpressionAttribute),"ITV");
            o.MapAttribute(typeof(StringLengthAttribute),"ITV");
            o.MapAttribute(typeof(RequiredAttribute), "ITV");
            o.AddTopicCallback(typeof(StringLengthAttribute), (attribute, s) =>
            {
                var sla = (StringLengthAttribute)attribute;
                if (sla.MinimumLength != 0 && s == "ValidationError")
                {
                    return "ValidationErrorIncludingMinimum";
                }
                
                return s;
            });
            options(o);
            services.AddSingleton(o);
            return o;
        }
    }
}
