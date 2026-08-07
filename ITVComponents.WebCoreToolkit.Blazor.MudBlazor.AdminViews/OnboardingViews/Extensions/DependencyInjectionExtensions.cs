using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Extensibility;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Extensions;

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
        // Client helper for the register-and-wait join flow (writes the same-browser join nonce cookie).
        services.AddToolkitClientScript("_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews/onboarding.js", true);
        // Zusatzangaben-Module: ohne konfigurierte Module ist das ein reiner Leerlauf - der Provider loest
        // den Plugin-Ladeweg erst auf, wenn wirklich Namen in den GlobalSettings stehen.
        services.TryAddScoped<ICustomCompanyInfoProvider, CustomCompanyInfoProvider>();

        if (partTypeLoadBehavior.ShouldLoadType(typeof(OnboardingHandler<>)))
        {
            services.AddScoped<IOnboardingHandler, OnboardingHandler<TContext>>();
        }

        if (partTypeLoadBehavior.ShouldLoadType(typeof(FlatTenantInvitationHandler<>)))
        {
            // Flat variant: employee invitations only (SupportsTenantInvitations == false).
            services.AddScoped<ITenantInvitationHandler, FlatTenantInvitationHandler<TContext>>();
            services.TryAddScoped<IInvitationMailComposer, InvitationMailComposer>();
        }

        if (partTypeLoadBehavior.ShouldLoadType(typeof(FlatOnboardingAdminHandler<>)))
        {
            services.AddScoped<IOnboardingAdminHandler, FlatOnboardingAdminHandler<TContext>>();
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
        // Client helper for the register-and-wait join flow (writes the same-browser join nonce cookie).
        services.AddToolkitClientScript("_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews/onboarding.js", true);
        // Zusatzangaben-Module: ohne konfigurierte Module ist das ein reiner Leerlauf - der Provider loest
        // den Plugin-Ladeweg erst auf, wenn wirklich Namen in den GlobalSettings stehen.
        services.TryAddScoped<ICustomCompanyInfoProvider, CustomCompanyInfoProvider>();

        if (partTypeLoadBehavior.ShouldLoadType(typeof(HierarchyOnboardingHandler<>)))
        {
            services.AddScoped<IOnboardingHandler, HierarchyOnboardingHandler<TContext>>();
        }

        if (partTypeLoadBehavior.ShouldLoadType(typeof(HierarchyTenantInvitationHandler<>)))
        {
            services.AddScoped<ITenantInvitationHandler, HierarchyTenantInvitationHandler<TContext>>();
            services.TryAddScoped<IInvitationMailComposer, InvitationMailComposer>();
        }

        if (partTypeLoadBehavior.ShouldLoadType(typeof(HierarchyOnboardingAdminHandler<>)))
        {
            services.AddScoped<IOnboardingAdminHandler, HierarchyOnboardingAdminHandler<TContext>>();
        }

        return services;
    }

    /// <summary>
    /// Registriert eigene Masken fuer Zusatzangaben-Module. Ein Modul nennt in seinem <c>ViewKey</c> nur
    /// den Schluessel; welche Komponente dahinter steht, entscheidet der Host hier.
    /// </summary>
    /// <example>
    /// <code>
    /// services.ConfigureCustomCompanyInfoViews(c => c.RegisterView&lt;NetworkTypeView&gt;("mlm.networktype"));
    /// </code>
    /// Die Komponente liest ihren Zustand ueber
    /// <c>[CascadingParameter] CustomCompanyInfoViewContext</c> und erfuellt <c>ICustomCompanyInfoView</c>.
    /// </example>
    public static IServiceCollection ConfigureCustomCompanyInfoViews(this IServiceCollection services,
        Action<CustomCompanyInfoViewConfiguration> configure)
    {
        return services.Configure(configure);
    }
}
