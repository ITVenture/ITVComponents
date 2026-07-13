using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Rendering;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Impl;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Extensions
{
    /// <summary>
    /// DI registrations for the HelpViews library. The help system is global (no Flat/Tree split): a single
    /// entry point wires the topic/resource/viewer handlers, the Markdown renderer and the reference resource
    /// store over the host's <c>IHelpSystemContext</c>. The routing assembly is registered by the shared
    /// AdminViews WebPart, so the admin pages and the anonymous viewer light up regardless.
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddMudBlazorHelpViews<TContext>(this IServiceCollection services,
            AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
            where TContext : DbContext, IHelpSystemContext
        {
            partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions { DefaultBehavior = TypeRegisterBehavior.Use };

            if (partTypeLoadBehavior.ShouldLoadType(typeof(HelpAdminHandler<>)))
            {
                services.AddScoped<IHelpAdminHandler, HelpAdminHandler<TContext>>();
            }

            if (partTypeLoadBehavior.ShouldLoadType(typeof(HelpResourceHandler<>)))
            {
                services.AddScoped<IHelpResourceHandler, HelpResourceHandler<TContext>>();
            }

            if (partTypeLoadBehavior.ShouldLoadType(typeof(HelpViewerHandler<>)))
            {
                services.AddScoped<IHelpViewerHandler, HelpViewerHandler<TContext>>();
            }

            services.TryAddScoped<IHelpContentRenderer, HelpContentRenderer>();
            // Reference resource store (EF blob). Host can override with its own IHelpResourceStore before this runs.
            services.TryAddScoped<IHelpResourceStore, HelpResourceBlobStore<TContext>>();

            return services;
        }
    }
}
