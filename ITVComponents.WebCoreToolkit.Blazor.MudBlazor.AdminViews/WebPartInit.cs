using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Flat;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Tree;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HelpExt = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Extensions.DependencyInjectionExtensions;
using HelpEndpoints = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.HelpViews.Endpoints.HelpResourceEndpoints;
using TsvExt = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Extensions.DependencyInjectionExtensions;
using FlatUserExt = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.Extensions.DependencyInjectionExtensions;
using TreeUserExt = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTreeTenantSecurityUserView.Extensions.DependencyInjectionExtensions;
using TscUserExt = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityContextUserView.Extensions.DependencyInjectionExtensions;
using OnboardingExt = ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Extensions.DependencyInjectionExtensions;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews;

// Consolidated from the former 5 Blazor.MudBlazor packages: TenantSecurityViews (TSV),
// AspNetCore(Tree)TenantSecurityUserView + TenantSecurityContextUserView (the 3 UserView strategies),
// and OnboardingViews. A single WebPart now registers the merged routing assembly and dispatches the
// strategy-specific DI extensions via (ActivationOptions.Identity, ActivationOptions.Strategy) — the same
// two axes the consolidated EF TenantSecurity part uses (Konsolidierung #18). The unified Users view/grid
// adapt their surface through IUserAdminHandler capability flags.
[WebPart]
public static class WebPartInit
{
    [LoadWebPartConfig]
    public static object? LoadOptions(IConfiguration config, string key, string path)
    {
        switch (key)
        {
            case "SecurityContext":
                return config.GetSection<SecurityContextOptions>(path);
            case "Activation":
                return config.GetSection<ActivationOptions>(path);
            case "Onboarding":
                return config.GetSection<OnboardingViewOptions>(path);
            case "Help":
                return config.GetSection<HelpViewOptions>(path);
        }

        return null;
    }

    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services,
        [WebPartConfig("SecurityContext")] SecurityContextOptions? securityOptions,
        [WebPartConfig("Activation")] ActivationOptions? activation,
        [WebPartConfig("Onboarding")] OnboardingViewOptions? onboardingOptions,
        [WebPartConfig("Help")] HelpViewOptions? helpOptions,
        [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
    {
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly, partTypeLoadBehavior);

        // Client-side assets emitted by the host's single <ITVentureReferences />.
        // BlazorMonaco powers the code editors (DiagnosticsQueries / Dashboard widgets / HealthScripts).
        services.AddToolkitClientScript("_content/BlazorMonaco/jsInterop.js");
        services.AddToolkitClientScript("_content/BlazorMonaco/lib/monaco-editor/min/vs/loader.js");
        services.AddToolkitClientScript("_content/BlazorMonaco/lib/monaco-editor/min/vs/editor/editor.main.js");
        // WidgetRenderer click-action delegation (asset ships with the base MudBlazor library).
        services.AddToolkitClientScript("_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor/widget-actions.js");

        //-- TenantSecurityViews (admin views) + the matching UserView strategy
        if (securityOptions is { ConfigureContext: true, ContextType: { Length: > 0 } contextTypeName })
        {
            var dic = new Dictionary<string, object>();
            var contextType = (Type)ExpressionParser.Parse(contextTypeName, dic);

            var identity = activation?.Identity ?? IdentityStrategy.CoreIdentity;
            var strategy = activation?.Strategy ?? TenantStrategy.Flat;

            if (identity == IdentityStrategy.CoreIdentity && strategy == TenantStrategy.Tree)
            {
                // Hierarchical ASP.NET-Core-Identity (AspNetTreeSecurityContext)
                var treeViews = typeof(TreeUserExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(contextType, nameof(TreeUserExt.AddMudBlazorTreeTenantSecurityViews));
                var treeUsers = typeof(TreeUserExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(contextType, nameof(TreeUserExt.AddMudBlazorTreeTenantSecurityUserView));
                treeViews(services, partTypeLoadBehavior);
                treeUsers(services, partTypeLoadBehavior);
            }
            else if (identity == IdentityStrategy.CoreIdentity)
            {
                // Flat ASP.NET-Core-Identity (AspNetSecurityContext)
                var tsv = typeof(TsvExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(contextType, nameof(TsvExt.AddMudBlazorTenantSecurityViews));
                var flatUsers = typeof(FlatUserExt).GetMethod<Action<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions>>(contextType, nameof(FlatUserExt.AddMudBlazorTenantSecurityUserView));
                tsv(services, partTypeLoadBehavior);
                flatUsers(services, partTypeLoadBehavior);
            }
            else
            {
                // Basic tenant-security (int-keyed SecurityContext) — admin views are key-type generic, the user view is its own
                var tsv = typeof(TsvExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(contextType, nameof(TsvExt.AddMudBlazorTenantSecurityViews));
                var tscUsers = typeof(TscUserExt).GetMethod<Action<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?>>(contextType, nameof(TscUserExt.AddMudBlazorTscUserView));
                tsv(services, partTypeLoadBehavior);
                tscUsers(services, partTypeLoadBehavior);
            }
        }

        //-- Onboarding views (optional, separate config). The host's DbContext picks the strategy:
        // tree-typed contexts route through the hierarchy extension, flat-typed ones through the regular one.
        if (onboardingOptions is { ConfigureContext: true, ContextType: { Length: > 0 } onboardingContextTypeName })
        {
            var dic = new Dictionary<string, object>();
            var contextType = (Type)ExpressionParser.Parse(onboardingContextTypeName, dic);

            if (typeof(IHierarchySecurityContextWithOnboarding).IsAssignableFrom(contextType))
            {
                var method = typeof(OnboardingExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(OnboardingExt.AddMudBlazorHierarchyOnboardingViews));
                method(services, partTypeLoadBehavior);
            }
            else if (typeof(ISecurityContextWithOnboarding).IsAssignableFrom(contextType))
            {
                var method = typeof(OnboardingExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                    contextType, nameof(OnboardingExt.AddMudBlazorOnboardingViews));
                method(services, partTypeLoadBehavior);
            }
        }

        //-- Help system (global, optional). Wires the topic/resource/viewer handlers + renderer + resource store
        // over the host context. The context type is the shared SecurityContext one (a help context is always at
        // least an ICoreSystemContext), so it is not configured separately — help just needs its enable flag.
        if (helpOptions is { ConfigureContext: true }
            && securityOptions is { ContextType: { Length: > 0 } helpContextTypeName }
            && TryResolveHelpContext(helpContextTypeName, out var helpContextType))
        {
            var method = typeof(HelpExt).GetMethod<Func<IServiceCollection, AssemblyPartTypeLoadBehaviorOptions?, IServiceCollection>>(
                helpContextType, nameof(HelpExt.AddMudBlazorHelpViews));
            method(services, partTypeLoadBehavior);
        }
    }

    [EndpointRegistrationMethod]
    public static void RegisterEndpoints(WebApplication app,
        [WebPartConfig("SecurityContext")] SecurityContextOptions? securityOptions,
        [WebPartConfig("Help")] HelpViewOptions? helpOptions)
    {
        // The anonymous resource resolver endpoint (/help/res/{name}) — closed over the host help context (taken
        // from SecurityContextOptions.ContextType, same as the service registration above).
        if (helpOptions is { ConfigureContext: true }
            && securityOptions is { ContextType: { Length: > 0 } helpContextTypeName }
            && TryResolveHelpContext(helpContextTypeName, out var helpContextType))
        {
            var map = typeof(HelpEndpoints).GetMethod<Func<IEndpointRouteBuilder, IEndpointRouteBuilder>>(
                helpContextType, nameof(HelpEndpoints.MapHelpResourceEndpoints));
            map(app);
        }
    }

    // Resolves the configured context type and confirms it actually hosts the help tables. Guards against a host
    // that enables help but whose DbContext does not implement IHelpSystemContext (help is skipped, not crashed).
    private static bool TryResolveHelpContext(string contextTypeName, out Type contextType)
    {
        contextType = (Type)ExpressionParser.Parse(contextTypeName, new Dictionary<string, object>());
        return typeof(IHelpSystemContext).IsAssignableFrom(contextType);
    }
}
