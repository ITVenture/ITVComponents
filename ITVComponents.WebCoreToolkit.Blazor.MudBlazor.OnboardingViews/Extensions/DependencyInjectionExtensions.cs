using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding;
using ITVComponents.WebCoreToolkit.EntityFramework.TreeCustomerOnboarding;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.Handlers;
using ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.Handlers.Impl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.Extensions;

/// <summary>
/// DI registrations for the OnboardingViews Blazor library. The host picks the strategy
/// at startup by calling either <see cref="AddMudBlazorOnboardingViews{TContext}"/> (flat,
/// <c>ISecurityContextWithOnboarding</c>) or <see cref="AddMudBlazorHierarchyOnboardingViews{TContext}"/>
/// (tree, <c>IHierarchySecurityContextWithOnboarding</c>). Both register the same routing
/// assembly so the <c>/Account/Onboarding/*</c> pages light up either way.
/// </summary>
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMudBlazorOnboardingViews<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
        where TContext : DbContext, ISecurityContextWithOnboarding
    {
        partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
        {
            DefaultBehavior = TypeRegisterBehavior.Use
        };

        services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);

        if (partTypeLoadBehavior.ShouldLoadType(typeof(OnboardingHandler<>)))
        {
            services.AddScoped<IOnboardingHandler, OnboardingHandler<TContext>>();
        }

        return services;
    }

    public static IServiceCollection AddMudBlazorHierarchyOnboardingViews<TContext>(this IServiceCollection services, AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior = null)
        where TContext : DbContext, IHierarchySecurityContextWithOnboarding
    {
        partTypeLoadBehavior ??= new AssemblyPartTypeLoadBehaviorOptions
        {
            DefaultBehavior = TypeRegisterBehavior.Use
        };

        services.AddBlazorRoutingAssembly(typeof(DependencyInjectionExtensions).Assembly, partTypeLoadBehavior);

        if (partTypeLoadBehavior.ShouldLoadType(typeof(HierarchyOnboardingHandler<>)))
        {
            services.AddScoped<IOnboardingHandler, HierarchyOnboardingHandler<TContext>>();
        }

        return services;
    }
}
