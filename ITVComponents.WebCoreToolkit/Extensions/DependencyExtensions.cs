using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.AspExtensions.Factories;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.Configuration.Impl;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect.Impl;
using ITVComponents.WebCoreToolkit.Globalization;
using ITVComponents.WebCoreToolkit.Localization;
using ITVComponents.WebCoreToolkit.Navigation;
using ITVComponents.WebCoreToolkit.Options;
using ITVComponents.WebCoreToolkit.Routing;
using ITVComponents.WebCoreToolkit.Routing.Impl;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.AssetLevelImpersonation;
using ITVComponents.WebCoreToolkit.Security.ClaimsTransformation;
using ITVComponents.WebCoreToolkit.Security.PermissionHandling;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using ITVComponents.WebCoreToolkit.Security.UserMappers;
using ITVComponents.WebCoreToolkit.Security.UserScopes;
using ITVComponents.WebCoreToolkit.WebPlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.Initialization;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins;
using ITVComponents.WebCoreToolkit.WebPlugins.InjectablePlugins.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Enables the culture prefix <c>/c/{culture}/…</c>: configures it and puts
        /// <see cref="CulturePathRequestCultureProvider"/> in FRONT of the other request-culture providers,
        /// so a language chosen in a URL beats the cookie and <c>Accept-Language</c>.
        /// <para>
        /// The pipeline half belongs to <c>app.UseCulturePath()</c>, which has to run as the very first
        /// middleware. Both are needed; neither works alone.
        /// </para>
        /// <para>
        /// This configures the <c>RequestLocalizationOptions</c> taken from DI - the ones
        /// <c>app.UseRequestLocalization()</c> uses when called without arguments. A host that passes its
        /// own options instance to <c>UseRequestLocalization()</c> has to insert the provider there
        /// itself; the middleware says so in the log when it notices.
        /// </para>
        /// </summary>
        /// <param name="services">the services to configure</param>
        /// <param name="options">an optional configuration callback for the culture path</param>
        /// <returns>the service collection for chaining</returns>
        public static IServiceCollection AddCulturePath(this IServiceCollection services, Action<CulturePathOptions> options = null)
        {
            if (options != null)
            {
                services.Configure(options);
            }

            services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(o =>
            {
                if (!o.RequestCultureProviders.OfType<CulturePathRequestCultureProvider>().Any())
                {
                    o.RequestCultureProviders.Insert(0, new CulturePathRequestCultureProvider());
                }
            });

            return services;
        }

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
            return services.AddHttpContextAccessor()
                .AddScoped<DefaultContextUserProvider>()
                .AddScoped<IHttpContextUserProvider>(sp => sp.GetRequiredService<DefaultContextUserProvider>())
                .AddScoped<IContextUserProvider>(sp => sp.GetRequiredService<DefaultContextUserProvider>());
        }

        /// <summary>
        /// Enables objects to get services injected that are loaded using a pluginFactory
        /// </summary>
        /// <param name="services">the servicesCollection in which the JnjectedPlugins are injected</param>
        /// <param name="options">the options used to configure custom plugin-loads</param>
        public static IServiceCollection UseInjectablePlugins(this IServiceCollection services, Action<InjectablePluginOptions> options)
        {
            return services.Configure(options)
                .AddScoped(typeof(IInjectablePlugin<>), typeof(InjectablePluginImpl<>))
                // Blazor-taugliche Variante: jede Lease oeffnet einen frischen, aufrufer-besessenen Lade-Scope.
                .AddScoped(typeof(IFreshInjectablePlugin<>), typeof(FreshInjectablePluginImpl<>));
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
            services.AddScoped<IImpersonationControl, DefaultAssetImpersonator>();
            services.UseSharedAssetPathContext();
            if (!collectable)
            {
                return services.AddScoped<IClaimsTransformation, AssetDrivenClaimsTransformation>();
            }

            return services.AddScoped<ICollectedClaimsProvider, AssetDrivenClaimsTransformation>();
        }

        /// <summary>
        /// Registers the host-neutral shared-asset context every consumer of a shared asset reads from
        /// (claims transformation, permission check, link building). Idempotent, and implied by
        /// <see cref="UseAssetDrivenClaimsTransformation"/> - a host that switched <c>UseSharedAssets</c> on
        /// in its WebPart configuration does not have to call it.
        /// </summary>
        /// <param name="services">the services to inject the context into</param>
        /// <param name="options">an optional callback configuring the path options</param>
        /// <returns>the provided servicecollection</returns>
        public static IServiceCollection UseSharedAssetPathContext(this IServiceCollection services,
            Action<SharedAssetPathOptions> options = null)
        {
            services.AddHttpContextAccessor();
            if (options != null)
            {
                services.Configure(options);
            }

            services.TryAddScoped<ISharedAssetContext, SharedAssetContext>();
            // Der Weg von der Detail-Seite zum Teilen-Knopf im Mantel: die Seite meldet hier die Werte,
            // die sie ohnehin dem Riegel erklaert, und der Knopf liest sie beim Klick.
            services.TryAddScoped<IShareArgumentSource, ShareArgumentSource>();
            // Die Grundfassung der Registry, damit ein Endpunkt sie in JEDEM Host in den Konstruktor
            // nehmen kann. Wer die Deklarationen behalten will, ueberschreibt sie mit der
            // persistierenden Fassung aus dem EF-Paket.
            services.TryAddSingleton<IAssetArgumentRegistry, InMemoryAssetArgumentRegistry>();
            services.TryAddScoped<IAssetAccessLog, NullAssetAccessLog>();
            return services;
        }

        /// <summary>
        /// Registriert den Riegel am Ausgang fuer den MVC-Weg: laeuft eine Anfrage in einem geteilten
        /// Asset, dessen Vorlage eine Bestaetigung der Argumente verlangt, wird die Antwort ohne
        /// Bestaetigung nicht ausgeliefert.
        /// <para>
        /// Damit muss kein Endpunkt daran denken - wer es vergisst, liefert nichts aus statt zu viel. Fuer
        /// den Blazor-Weg gibt es kein Gegenstueck an dieser Stelle; dort uebernimmt die Komponente
        /// <c>AssetScope</c> die Render-Grenze.
        /// </para>
        /// </summary>
        /// <param name="services">die Dienstsammlung</param>
        /// <returns>die uebergebene Dienstsammlung</returns>
        public static IServiceCollection UseSharedAssetGuard(this IServiceCollection services)
        {
            return services.UseSharedAssetPathContext()
                .Configure<MvcOptions>(o => o.Filters.Add<SharedAssetGuardFilter>());
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
            return ConfigureLocalization(services, options, "Resources");
        }

        /// <summary>
        /// Initializes the WebApplications language settings with a custom resources path.
        /// </summary>
        /// <param name="services">the service-collection where ot inject the settings</param>
        /// <param name="options">the options-configurator</param>
        /// <param name="resourcesPath">the path that <see cref="Microsoft.Extensions.Localization.LocalizationOptions.ResourcesPath"/> is set to.
        /// Pass <c>null</c> or empty to leave it unset — the resource type's <c>FullName</c> is then used as-is for the resource base name.</param>
        /// <returns>a the serviceCollection instance that was passed as argument</returns>
        public static IServiceCollection ConfigureLocalization(this IServiceCollection services, Action<CultureOptions> options, string? resourcesPath)
        {
            return services.Configure(options).AddLocalization(l =>
            {
                if (!string.IsNullOrEmpty(resourcesPath))
                {
                    l.ResourcesPath = resourcesPath;
                }
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
            // Der Formatter setzt den Asset-Abschnitt in die Scope-Platzhalter ein und braucht dafuer den
            // Kontext. Er wird hier mitregistriert, weil der eingebaute DI-Container Standardwerte von
            // Konstruktor-Parametern NICHT beruecksichtigt - ein Host ohne geteilte Assets bekaeme sonst
            // beim Aufloesen einen Fehler statt eines leeren Kontexts.
            return services.UseSharedAssetPathContext()
                .AddScoped<IUrlFormat, UrlFormatImpl>();
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
            o.MapAttribute(typeof(CompareAttribute), "ITV");
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
            if (Attribute.IsDefined(impl, typeof(ExplicitlyExposeAttribute)) || Attribute.IsDefined(svc, typeof(ExplicitlyExposeAttribute)))
            {
                var att = (ExplicitlyExposeAttribute)(Attribute.GetCustomAttribute(impl, typeof(ExplicitlyExposeAttribute)) ??
                          Attribute.GetCustomAttribute(svc, typeof(ExplicitlyExposeAttribute)));
                var exposeAnyway = att.ExposeEntireTree;
                foreach (var ifs in impl.GetInterfaces().Union(impl.GetBaseTypes()).Where(it =>
                             (exposeAnyway || Attribute.IsDefined(it, typeof(ExplicitlyExposeAttribute))) &&
                             it != svc && it != impl && !it.IsGenericTypeDefinition))
                {
                    lifetimeCallback(services, ifs, sp => sp.GetService(svc));
                    Console.WriteLine($"Registering {ifs} for {svc}");
                }
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
            return RegisterExplicityInterfacesTransient<TService, TService>(services);
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
            return RegisterExplicityInterfacesSingleton<TService, TService>(services);
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

        /// <summary>
        /// Registers a factory object that enables Pages to load custom implementations of their pageModel by exposing the logic via an IPageHandlerInstance implementation
        /// </summary>
        /// <param name="services">the serviceCollection where the required services are being injected</param>
        /// <returns>the provided ServiceCollection for method chaining</returns>
        public static IServiceCollection UsePageModelHandlerFactory(this IServiceCollection services, Action<PageHandlerFactoryOptions> configure = null)
        {
            services.AddSingleton<IPageModelFactory, PageModelFactory>()
                .AddScoped(typeof(IPageHandlerProvider<,>), typeof(FinalPageHandler<,>));
            if (configure != null)
            {
                ConfigurePageModelHandlerFactory(services, configure);
            }

            return services;
        }

        public static IServiceCollection ConfigurePageModelHandlerFactory(this IServiceCollection services,
            Action<PageHandlerFactoryOptions> configure)
        {
            return services.Configure(configure);
        }

        public static IServiceCollection UseOAuthClientFactory(this IServiceCollection services)
        {
            return services.AddScoped<IOAuthTokenService, OAuthTokenService>()
                .AddScoped<IOAuthHttpClientFactory, OAuthHttpClientFactory>();
        }
    }
}
