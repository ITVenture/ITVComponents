using System.Text.Json;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Antiforgery;
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
        var manageGroup = endpoints.MapGroup("/Account/Manage").RequireAuthorization();

        manageGroup.MapPost("/DownloadPersonalData", async (HttpContext httpContext) =>
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

        // Passkey creation options (registration flow): requires an authenticated user — returns JSON parsed
        // by `PublicKeyCredential.parseCreationOptionsFromJSON` on the client side. Explicit antiforgery
        // validation because the JS fetch sends the token via header, not form field.
        manageGroup.MapPost("/PasskeyCreationOptions", async (HttpContext httpContext) =>
        {
            var services = httpContext.RequestServices;
            var antiforgery = services.GetRequiredService<IAntiforgery>();
            await antiforgery.ValidateRequestAsync(httpContext);

            var handlerProvider = services
                .GetRequiredService<IPageHandlerProvider<PasskeyPageModel, IPasskeyHandler>>();
            var handler = handlerProvider.Handler;
            if (!handler.UsePage)
            {
                return Results.NotFound();
            }

            var optionsJson = await handler.MakePasskeyCreationOptionsAsync(httpContext.User);
            if (optionsJson is null)
            {
                return Results.NotFound();
            }

            return Results.Content(optionsJson, contentType: "application/json");
        });

        // Passkey request options (sign-in flow): anonymous — returns JSON parsed by
        // `PublicKeyCredential.parseRequestOptionsFromJSON`. Username may be empty (conditional UI / passkey
        // autofill). The handler / Identity APIs return synthetic options to prevent username enumeration.
        var accountGroup = endpoints.MapGroup("/Account");
        accountGroup.MapPost("/PasskeyRequestOptions", async (HttpContext httpContext, string? username) =>
        {
            var services = httpContext.RequestServices;
            var antiforgery = services.GetRequiredService<IAntiforgery>();
            await antiforgery.ValidateRequestAsync(httpContext);

            var handlerProvider = services
                .GetRequiredService<IPageHandlerProvider<PasskeyPageModel, IPasskeyHandler>>();
            var handler = handlerProvider.Handler;
            if (!handler.UsePage)
            {
                return Results.NotFound();
            }

            var optionsJson = await handler.MakePasskeyRequestOptionsAsync(username);
            return Results.Content(optionsJson, contentType: "application/json");
        });

        return endpoints;
    }
}
