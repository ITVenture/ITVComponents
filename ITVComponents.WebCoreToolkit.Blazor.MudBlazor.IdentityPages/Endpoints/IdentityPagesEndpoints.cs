using System.Text.Json;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Endpoints;

public static class IdentityPagesEndpoints
{
    public static IEndpointRouteBuilder MapMudBlazorIdentityPagesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/Account/Manage").RequireAuthorization();

        group.MapPost("/DownloadPersonalData", async (HttpContext httpContext) =>
        {
            var services = httpContext.RequestServices;
            var handlerProvider = services
                .GetRequiredService<IPageHandlerProvider<DownloadPersonalDataModel, IDownloadPersonalDataHandler>>();
            var logger = services.GetRequiredService<ILogger<IDownloadPersonalDataHandler>>();
            var localizer = services.GetRequiredService<IStringLocalizer<IdentityMessages>>();

            var handler = handlerProvider.Handler;
            if (!handler.UsePage)
            {
                return Results.NotFound();
            }

            var user = await handler.FetchUser(httpContext.User);
            try
            {
                if (!user.UserExists)
                {
                    return Results.NotFound(localizer["Unable to load user with ID '{0}'.",
                        handler.GetUserId(httpContext.User)].Value);
                }

                var userId = handler.GetUserId(httpContext.User);
                logger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                var personalData = await handler.FetchPersonalData(user);
                return Results.File(JsonSerializer.SerializeToUtf8Bytes(personalData),
                    contentType: "application/json",
                    fileDownloadName: "PersonalData.json");
            }
            finally
            {
                handler.ReleaseUser(user);
            }
        }).DisableAntiforgery();

        return endpoints;
    }
}
