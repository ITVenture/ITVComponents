using System.Reflection;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Configuration;
using ITVComponents.WebCoreToolkit.Blazor.Localization;
using ITVComponents.WebCoreToolkit.Blazor.Resources;
using ITVComponents.WebCoreToolkit.Blazor.Routing;
using ITVComponents.WebCoreToolkit.Blazor.Security;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Registers <see cref="IAttributeMessageLocalizer"/> so that the
        /// <c>ToolkitDataAnnotationsValidator</c> component can translate topic-prefixed
        /// DataAnnotation error messages on Blazor models. Requires
        /// <c>ConfigureAttributeTranslation()</c> to have been called beforehand so that
        /// <see cref="ITVComponents.WebCoreToolkit.Options.AttributeTranslationOptions"/> is in the container.
        /// </summary>
        /// <param name="services">the service collection to register into</param>
        /// <returns>the service collection for chaining</returns>
        public static IServiceCollection UseBlazorAttributeMessages(this IServiceCollection services)
        {
            services.AddSingleton<IAttributeMessageLocalizer, AttributeMessageLocalizer>();
            return services;
        }

        /// <summary>
        /// Registers the <see cref="ICultureSwitcher"/> a language picker builds on. Circuit-scoped,
        /// because it answers from the address of the page currently showing.
        /// <para>
        /// Belongs to a host that runs <c>services.AddCulturePath()</c> and <c>app.UseCulturePath()</c>:
        /// the switcher exchanges the URL's culture prefix and reloads. Without the culture path there is
        /// no prefix to exchange, and the picker would have nothing to do.
        /// </para>
        /// </summary>
        /// <param name="services">the service collection to register into</param>
        /// <returns>the service collection for chaining</returns>
        public static IServiceCollection AddCultureSwitcher(this IServiceCollection services)
        {
            services.AddScoped<ICultureSwitcher, CultureSwitcher>();
            return services;
        }

        /// <summary>
        /// Registers the circuit-scoped <see cref="BlazorContextUserProvider"/> as the host-neutral
        /// <see cref="IContextUserProvider"/> for a Blazor host. Place a single
        /// <c>&lt;ContextUserInitializer /&gt;</c> near the application root so the synchronous
        /// <see cref="IContextUserProvider.User"/> getter is seeded after the first interactive render.
        /// </summary>
        /// <param name="services">the service collection to register into</param>
        /// <returns>the service collection for chaining</returns>
        public static IServiceCollection AddBlazorContextUser(this IServiceCollection services)
        {
            // Needed for the static-SSR fallback in BlazorContextUserProvider.User (Identity pages render
            // without a circuit, so the AuthenticationStateProvider yields Anonymous there). Idempotent.
            services.AddHttpContextAccessor();
            services.AddScoped<BlazorContextUserProvider>();
            services.AddScoped<IContextUserProvider>(sp => sp.GetRequiredService<BlazorContextUserProvider>());
            return services;
        }

        /// <summary>
        /// Registers the circuit-scoped <see cref="ScopedPermissionScope"/> as the <see cref="IPermissionScope"/>
        /// for a Blazor host. Because the registration is scoped, one instance lives per circuit == per browser
        /// tab, so different tabs can carry different tenants at the same time without a browser-wide cookie.
        /// The tenant is taken from the route/query (<see cref="ScopedPermissionScopeOptions.RouteOverrideParam"/>)
        /// and validated against the user's eligible scopes by the shared resolution engine. Requires
        /// <see cref="AddBlazorContextUser"/> (the scope resolves the ambient user/route via
        /// <see cref="IContextUserProvider"/>).
        /// </summary>
        /// <param name="services">the service collection to register into</param>
        /// <param name="options">configures the scoped-permission-scope options</param>
        /// <returns>the service collection for chaining</returns>
        public static IServiceCollection AddBlazorPermissionScope(this IServiceCollection services, Action<ScopedPermissionScopeOptions> options)
        {
            services.AddHttpContextAccessor();
            return services.Configure(options)
                .AddScoped<IPermissionScope, ScopedPermissionScope>();
        }

        /// <summary>
        /// Registers the <see cref="TenantPathPrefixMiddleware"/> in the request pipeline. Validates the
        /// first URL path segment against the authenticated user's eligible scopes (via
        /// <see cref="ISecurityRepository.GetEligibleScopes"/>): unknown/ineligible segments yield 404, the
        /// root ("/") redirects to the user's default scope, auth and Blazor-internal paths pass through.
        /// No-op when <see cref="ScopedPermissionScopeOptions.TenantSource"/> is not
        /// <see cref="TenantSource.PathSegment"/>. Place AFTER <c>UseAuthentication</c>/<c>UseAuthorization</c>
        /// and BEFORE <c>MapRazorComponents</c>.
        /// </summary>
        /// <param name="app">the application builder</param>
        /// <returns>the application builder for chaining</returns>
        public static IApplicationBuilder UseTenantPathPrefix(this IApplicationBuilder app)
            => app.UseMiddleware<TenantPathPrefixMiddleware>();

        public static IServiceCollection ConfigureStubComponents(this IServiceCollection services, Action<StubComponentConfiguration> configure)
        {
            return services.Configure(configure);
        }

        /// <summary>
        /// Adds the given assembly to <see cref="BlazorRoutingOptions.AdditionalAssemblies"/>
        /// so the host's Blazor Router picks up its routable components.
        /// Idempotent: calling twice with the same assembly is a no-op.
        /// </summary>
        public static IServiceCollection AddBlazorRoutingAssembly(this IServiceCollection services, Assembly assembly)
        {
            services.Configure<BlazorRoutingOptions>(o => o.AddAssembly(assembly));
            return services;
        }

        /// <summary>
        /// Adds the given assembly with a per-type filter. The filter is consumed at
        /// render-time by <c>FilteredRouteView</c> to short-circuit excluded pages
        /// to NotFound. Mirrors the MVC <c>AssemblyPartWithGenerics</c> blacklist
        /// so the same JSON config can be reused.
        /// </summary>
        public static IServiceCollection AddBlazorRoutingAssembly(
            this IServiceCollection services,
            Assembly assembly,
            AssemblyPartTypeLoadBehaviorOptions? typeFilter)
        {
            services.Configure<BlazorRoutingOptions>(o => o.AddAssembly(assembly, typeFilter));
            return services;
        }

        /// <summary>
        /// Registers a client-side script that the host emits via <c>&lt;ITVentureReferences /&gt;</c>.
        /// Call from a library's <c>WebPartInit</c> so the toolkit collects the union of all required
        /// scripts. Duplicate URLs are ignored.
        /// </summary>
        public static IServiceCollection AddToolkitClientScript(this IServiceCollection services, string src, bool module = false, bool defer = false, bool async = false)
        {
            services.Configure<ClientResourceOptions>(o => o.AddScript(src, module, defer, async));
            return services;
        }

        /// <summary>
        /// Registers a client-side stylesheet that the host emits via <c>&lt;ITVentureReferences /&gt;</c>.
        /// Call from a library's <c>WebPartInit</c>. Duplicate URLs are ignored.
        /// </summary>
        public static IServiceCollection AddToolkitClientStyleSheet(this IServiceCollection services, string href)
        {
            services.Configure<ClientResourceOptions>(o => o.AddStyleSheet(href));
            return services;
        }
    }
}
