using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Blazor.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages;

[WebPart]
public static class WebPartInit
{
    [ServiceRegistrationMethod]
    public static void RegisterServices(IServiceCollection services)
    {
        services.AddBlazorRoutingAssembly(typeof(WebPartInit).Assembly);
    }

    [EndpointRegistrationMethod]
    public static void RegisterEndpoints(WebApplication builder)
    {
        builder.MapMudBlazorIdentityPagesEndpoints();
    }
}
