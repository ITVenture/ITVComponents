using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.AspExtensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Endpoints;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Services.Impl;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages;

[WebPart]
public static class WebPartInit
{
    // Mirrors the MVC TelerikUi.AspNetCoreIdentityPages WebPartInit: a single
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

        if (options.UseDefaultMailSender)
        {
            services.AddSingleton<IEmailSender, DefaultMailSender>();
        }
    }

    [EndpointRegistrationMethod]
    public static void RegisterEndpoints(WebApplication builder)
    {
        builder.MapMudBlazorIdentityPagesEndpoints();
    }
}
