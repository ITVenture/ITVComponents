using ITVComponents.Logging;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Endpoints;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers.Impl;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.IdentityShared.Options;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages;

[WebPart]
public static class WebPartInit
{
    // Mirrors the MVC TelerikUi.IdentityPages WebPartInit: a single
    // DetailConfigPath drives this part — WebPartManager dispatches the 2-arg
    // overload and stores the result under the "DEFAULT" key.
    [LoadWebPartConfig]
    public static object? LoadOptions(IConfiguration config, string path)
    {
        return config.GetSection<IdentityUiOptions>(path);
    }

    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services,
        [WebPartConfig] IdentityUiOptions? options,
        [WebPartConfig(Global.PartTypeLoadBehaviorOption)] AssemblyPartTypeLoadBehaviorOptions? partTypeLoadBehavior)
    {
        services.AddToolkitClientScript(
            "_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages/PasskeySubmit.js", true);
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly, partTypeLoadBehavior);

        if (options is null)
        {
            return;
        }

        // Note: the MVC variant also registers IdentityUserGuard<TUser>, but that type is
        // internal to the TelerikUi.ANCIP assembly and the Blazor pages don't inject UserGuard<>,
        // so we skip that step here.

        services.Configure<LoginOptions>(op =>
        {
            op.UserNameIsEmail = options.UserNameIsEmail;
            op.UseLocalAccounts = options.UseLocalAccounts;
            op.RegistrationPage = options.RegistrationPage;
            if (options.UseExternalLogins)
            {
                op.ExternalLoginPage = options.ExternalLoginPage;
            }
        });

        // IPasskeyHandler is consumed by Blazor pages (Login/Passkeys/RenamePasskey) and the
        // MapMudBlazorIdentityPagesEndpoints endpoints — but ANCIP doesn't know about it, so we
        // wire it here. Generic variant when an IdentityUserType is configured; falls back to
        // PasskeyHandler (UsePage=false) when TUser can't be resolved.
        //
        // ZUSAETZLICH an IdentityUiOptions.UsePasskeys gebunden, und das laesst sich NICHT ableiten: der
        // EF-Benutzer-Speicher setzt die Passkey-Methoden immer um, also meldet SupportsUserPasskey auch
        // dann true, wenn der DbContext die Passkey-Entitaet nicht abbildet - und .NET 10 schliesst sie
        // standardmaessig aus. Ohne den Schalter zeigte die Kontoverwaltung einen Abschnitt, der erst beim
        // Speichern in der Datenbank scheitert. Wer ihn setzt, bildet die Entitaet auch selbst ab.
        if (options.RegisterPageHandlers
            && options.UsePasskeys
            && !string.IsNullOrEmpty(options.IdentityUserType)
            && Type.GetType(options.IdentityUserType) is { } tuserType)
        {
            services.ConfigurePageModelHandlerFactory(ha =>
            {
                ha.ConfigureHandlerType(typeof(PasskeyPageModel), typeof(IPasskeyHandler), typeof(PasskeyHandler<>), false);
                ha.ConfigureGenericArgument("TUser", tuserType);
            });
        }
        else
        {
            services.ConfigurePageModelHandlerFactory(ha =>
            {
                ha.ConfigureHandlerType<PasskeyPageModel, IPasskeyHandler, PasskeyHandler>(false);
            });
        }

        // Mail-sender registration (IEmailSender -> DefaultMailSender, plus IAppMailSender) lives solely in
        // IdentityShared's WebPart, which owns the Identity-UI mail dependency. Doing it here too was redundant
        // (same singleton class) and is removed to keep a single source of truth.
    }

    [EndpointRegistrationMethod]
    public static void RegisterEndpoints(WebApplication builder)
    {
        builder.MapMudBlazorIdentityPagesEndpoints();
    }
}
