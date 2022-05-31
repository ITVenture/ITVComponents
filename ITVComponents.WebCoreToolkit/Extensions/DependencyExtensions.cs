using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Configuration.Impl;
using ITVComponents.WebCoreToolkit.DependencyInjection;
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

            return services.UseContextUserAccessor().AddScoped<IWebPluginHelper, WebPluginHelper>();
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
        public static IServiceCollection EnableRoleBaseAuthorization(this IServiceCollection services)
        {
            return services.UseContextUserAccessor()
                .AddSingleton<IAuthorizationPolicyProvider, ToolkitPolicyProvider>()
                .AddScoped<IAuthorizationHandler, AssignedPermissionsHandler>()
                .AddScoped<IAuthorizationHandler, FeatureActivatedHandler>();
        }

        /// <summary>
        /// Enables RoleBased Authorization for the current Web application
        /// </summary>
        /// <param name="services">the service collection for which to enable authorization</param>
        /// <param name="configuration">the IConfiguration object giving access to the application settings file</param>
        /// <param name="options">Clonfigures the options for the role-based Authorization handler</param>
        public static IServiceCollection EnableRoleBaseAuthorization(this IServiceCollection services, Action<ToolkitPolicyOptions> configure)
        {
            return services.Configure(configure)
                .EnableRoleBaseAuthorization();
        }

        /// <summary>
        /// Injects a service that provides the current user and all available http-context information
        /// </summary>
        /// <param name="services">the dependency enviornment where the service is injected</param>
        /// <returns>the same serviceCollection for method-chaining</returns>
        public static IServiceCollection UseContextUserAccessor(this IServiceCollection services)
        {
            return services.AddHttpContextAccessor().AddScoped<IContextUserProvider, DefaultContextUserProvider>();
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
        /// Uses Asset-Information to add required claims to the logged-in identity
        /// </summary>
        /// <param name="services">the services where transformation-provider is injected</param>
        /// <param name="collectable">indicates whether to inject the RepositoryClaims transformer in a way, that enables the usage of multiple transformers</param>
        public static IServiceCollection UseAssetDrivenClaimsTransformation(this IServiceCollection services,
            bool collectable = false)
        {
            if (!collectable)
            {
                return services.AddScoped<IClaimsTransformation, AssetDrivenClaimsTransformation>();
            }

            return services.AddScoped<ICollectedClaimsProvider, AssetDrivenClaimsTransformation>();
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

        /// <summary>
        /// Uses Data-Annotation Translations with the default settings
        /// </summary>
        /// <param name="services">the service-collection where ot inject the Attribute-Translation</param>
        /// <returns>the generated Translation options for the current application</returns>
        public static AttributeTranslationOptions ConfigureAttributeTranslation(this IServiceCollection services)
        {
            return ConfigureAttributeTranslation(services, o => { });
        }

        /// <summary>
        /// Uses Data-Annotation Translations with custom settings
        /// </summary>
        /// <param name="services">the service-collection where ot inject the Attribute-Translation</param>
        /// <param name="options">a callback that can be used to modify the default settings</param>
        /// <returns>the generated Translation options for the current application</returns>
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

        /// <summary>
        /// Initializes a service that is capable to process long-term actions in the background
        /// </summary>
        /// <param name="services">the service-collection where ot inject the background-service</param>
        /// <param name="queueCapacity">the capacity of the queue that will hold the background-tasks</param>
        /// <returns>the servicecollection instance that was passed as argument</returns>
        public static IServiceCollection UseBackgroundTasks(this IServiceCollection services, int queueCapacity = 100)
        {
            services.AddHostedService<BackgroundTaskProcessorService<IBackgroundTaskQueue,BackgroundTask>>();
            services.AddSingleton<IBackgroundTaskQueue>(ctx => new BackgroundTaskQueue(queueCapacity));
            return services;
        }

        /// <summary>
        /// Registers all interfaces implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <typeparam name="TImpl">the implemented type</typeparam>
        /// <param name="services">the services collection</param>
        /// <param name="lifetimeCallback">a callback that will be applied for registration on each interface</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfaces<TService, TImpl>(this IServiceCollection services,
            Func<IServiceCollection, Type, Func<IServiceProvider, object>, IServiceCollection> lifetimeCallback)
        {
            var impl = typeof(TImpl);
            var svc = typeof(TService);
            foreach (var ifs in impl.GetInterfaces().Union(impl.GetBaseTypes()).Where(it =>
                         Attribute.IsDefined(impl, typeof(ExplicitlyExposeAttribute)) && it != svc && it != impl && !it.IsGenericTypeDefinition))
            {
                lifetimeCallback(services, ifs, services => services.GetService(svc));
            }

            return services;
        }

        /// <summary>
        /// Registers all interfaces transient implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <typeparam name="TImpl">the implemented type</typeparam>
        /// <param name="services">the services collection</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfacesTransient<TService, TImpl>(this IServiceCollection services)
        {
            return RegisterExplicityInterfaces<TService, TImpl>(services, ServiceCollectionServiceExtensions.AddTransient);
        }

        /// <summary>
        /// Registers all interfaces transient implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <param name="services">the services collection</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfacesTransient<TService>(this IServiceCollection services)
        {
            return RegisterExplicityInterfacesScoped<TService, TService>(services);
        }

        /// <summary>
        /// Registers all interfaces singleton implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <typeparam name="TImpl">the implemented type</typeparam>
        /// <param name="services">the services collection</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfacesSingleton<TService, TImpl>(this IServiceCollection services)
        {
            return RegisterExplicityInterfaces<TService, TImpl>(services, ServiceCollectionServiceExtensions.AddSingleton);
        }

        /// <summary>
        /// Registers all interfaces singleton implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <param name="services">the services collection</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfacesSingleton<TService>(this IServiceCollection services)
        {
            return RegisterExplicityInterfacesScoped<TService, TService>(services);
        }

        /// <summary>
        /// Registers all interfaces scoped implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <typeparam name="TImpl">the implemented type</typeparam>
        /// <param name="services">the services collection</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfacesScoped<TService, TImpl>(this IServiceCollection services)
        {
            return RegisterExplicityInterfaces<TService, TImpl>(services, ServiceCollectionServiceExtensions.AddScoped);
        }

        /// <summary>
        /// Registers all interfaces scoped implemented by the TImpl and return the service injected with TService
        /// </summary>
        /// <typeparam name="TService">the service that was originally injected</typeparam>
        /// <param name="services">the services collection</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection RegisterExplicityInterfacesScoped<TService>(this IServiceCollection services)
        {
            return RegisterExplicityInterfacesScoped<TService, TService>(services);
        }
    }
}
