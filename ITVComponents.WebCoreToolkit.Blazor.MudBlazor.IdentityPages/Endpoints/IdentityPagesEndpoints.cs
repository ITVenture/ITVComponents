using System;
using System.Buffers.Text;
using System.IO;
using System.Linq;
using System.Text.Json;
using ITVComponents.WebCoreToolkit.AspExtensions.PageHandler;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Handlers;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
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

        // Gegenstueck zu PasskeyCreationOptions: nimmt das fertige Credential entgegen, prueft es und legt den
        // Schluessel ab. Muss ein Endpunkt sein, nicht ein Aufruf vom Circuit.
        //
        // MakePasskeyCreationOptionsAsync legt die Herausforderung in einem verschluesselten Cookie ab, und
        // PerformPasskeyAttestationAsync liest sie von dort wieder - beide Haelften brauchen also den
        // HttpContext, die eine zum Schreiben, die andere zum Lesen. Auf einem Blazor-Circuit gibt es keinen;
        // der Aufruf schlug dort mit "HttpContext must not be null" fehl.
        manageGroup.MapPost("/PasskeyAttestation", async (HttpContext httpContext) =>
        {
            var services = httpContext.RequestServices;
            var antiforgery = services.GetRequiredService<IAntiforgery>();
            await antiforgery.ValidateRequestAsync(httpContext);

            var handler = services
                .GetRequiredService<IPageHandlerProvider<PasskeyPageModel, IPasskeyHandler>>().Handler;
            if (!handler.UsePage)
            {
                return Results.NotFound();
            }

            var logger = services.GetRequiredService<ILogger<IPasskeyHandler>>();
            var localizer = services.GetRequiredService<IStringLocalizer<IdentityMessages>>();

            string credentialJson;
            using (var reader = new StreamReader(httpContext.Request.Body))
            {
                credentialJson = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(credentialJson))
            {
                logger.LogError("Passkey attestation called without a credential body.");
                return Results.BadRequest(localizer["Passkey attestation failed."].Value);
            }

            var attestation = await handler.PerformPasskeyAttestationAsync(credentialJson);
            if (!attestation.Succeeded || attestation.Passkey is null)
            {
                var reason = attestation.Failure?.Message ?? "no failure message";
                logger.LogError("Passkey attestation failed: {Reason}", reason);
                return Results.BadRequest(attestation.Failure?.Message
                                          ?? localizer["Passkey attestation failed."].Value);
            }

            var add = await handler.AddOrUpdateUserPasskeyAsync(httpContext.User, attestation.Passkey);
            if (!add.Succeeded)
            {
                var reason = string.Join(", ", add.Errors.Select(e => e.Description));
                logger.LogError("Passkey verified but not stored: {Reason}", reason);
                return Results.BadRequest(reason);
            }

            // Der Aufrufer braucht nur die Kennung, um auf die Benennungs-Seite weiterzugehen.
            return Results.Json(new { credentialId = Base64Url.EncodeToString(attestation.Passkey.CredentialId) });
        });

        // ---------------------------------------------------------------------------------------------------
        // Rueckspruenge, die ein Authentifizierungs-Cookie schreiben.
        //
        // Die Konto-Seiten rendern interaktiv und haben damit keinen HttpContext, in den sich ein Cookie
        // schreiben liesse. Sie erledigen ihre Arbeit auf dem Circuit und navigieren danach mit forceLoad
        // hierher: echte HTTP-Anfrage, Cookie geschrieben, Weiterleitung zurueck, Circuit kommt frisch hoch.
        //
        // returnUrl laeuft ueber Results.LocalRedirect - das wirft bei einer fremden Adresse, statt sie zu
        // besuchen. Ein offener Redirect an dieser Stelle waere besonders unangenehm: die Seite, von der aus
        // man umgeleitet wird, ist eine Kontoverwaltung.
        //
        // ZUM ZEITFENSTER: aendert die Seite den Security-Stamp (Passwort, 2FA), traegt diese Anfrage noch das
        // alte Cookie. Der SecurityStampValidator prueft nur alle 30 Minuten (Default), greift hier also
        // normalerweise nicht. Lag die letzte Cookie-Ausstellung laenger zurueck - wer ein Formular eine halbe
        // Stunde offen liegen laesst -, meldet er ab und der Benutzer landet auf der Anmeldung. Die Aenderung
        // ist dann trotzdem erfolgt; deshalb fuehren die Seiten ihre Statusmeldung in der returnUrl mit.
        // ---------------------------------------------------------------------------------------------------
        manageGroup.MapGet("/RefreshSignIn", async (HttpContext httpContext, string? returnUrl) =>
        {
            var services = httpContext.RequestServices;
            var handler = services
                .GetRequiredService<IPageHandlerProvider<SignInSessionModel, ISignInSessionHandler>>().Handler;

            if (handler.UsePage)
            {
                var user = await handler.FetchUser(httpContext.User);
                try
                {
                    await handler.RefreshSignIn(user);
                }
                finally
                {
                    handler.ReleaseUser(user);
                }
            }

            return Results.LocalRedirect(LocalOr(returnUrl, "/Account/Manage"));
        });

        manageGroup.MapGet("/SignOutSession", async (HttpContext httpContext, string? returnUrl) =>
        {
            var services = httpContext.RequestServices;
            var handler = services
                .GetRequiredService<IPageHandlerProvider<SignInSessionModel, ISignInSessionHandler>>().Handler;

            // Kein FetchUser: gerufen wird das, NACHDEM das Konto geloescht wurde - den Benutzer gibt es nicht mehr.
            await handler.SignOut();
            return Results.LocalRedirect(LocalOr(returnUrl, "/"));
        });

        manageGroup.MapGet("/ForgetBrowser", async (HttpContext httpContext, string? returnUrl) =>
        {
            var services = httpContext.RequestServices;
            var handler = services
                .GetRequiredService<IPageHandlerProvider<SignInSessionModel, ISignInSessionHandler>>().Handler;

            await handler.ForgetTwoFactorClient();
            return Results.LocalRedirect(LocalOr(returnUrl, "/Account/Manage/TwoFactorAuthentication"));
        });

        // Hinweg zum externen Anbieter: Abmelden vom externen Schema und Challenge. Beides schreibt bzw. loescht
        // Cookies und leitet den Browser zum Anbieter um - eine echte Umleitung, die es auf einem Circuit nicht
        // gibt. Die Konto-Seite ruft das mit forceLoad auf.
        manageGroup.MapGet("/LinkLogin", async (HttpContext httpContext, string provider) =>
        {
            var services = httpContext.RequestServices;
            var handler = services
                .GetRequiredService<IPageHandlerProvider<ExternalLoginsModel, IExternalLoginsHandler>>().Handler;

            if (!handler.UsePage || string.IsNullOrEmpty(provider))
            {
                return Results.LocalRedirect("/Account/Manage/ExternalLogins");
            }

            await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            var redirectUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}/Account/Manage/ExternalLoginsCallback";
            var properties = handler.ConfigureExternalAuthenticationProperties(provider, redirectUrl, httpContext.User);
            if (properties == null)
            {
                var logger = services.GetRequiredService<ILogger<IExternalLoginsHandler>>();
                logger.LogError("No authentication properties for external provider '{Provider}' — challenge skipped.", provider);
                return Results.LocalRedirect("/Account/Manage/ExternalLogins");
            }

            return Results.Challenge(properties, new[] { provider });
        });

        // Rueckweg des externen Anbieters. War fruehe eine Razor-Seite ohne jedes Markup - reine Durchgangs-
        // station, die nur verarbeitet und weiterleitet. Als Endpunkt entfaellt damit die letzte static-SSR-
        // Seite im sichtbaren Konto-Bereich; die Adresse bleibt dieselbe, damit die beim Challenge gesetzte
        // Rueckkehr-URL weiter passt.
        manageGroup.MapGet("/ExternalLoginsCallback", async (HttpContext httpContext) =>
        {
            var services = httpContext.RequestServices;
            var localizer = services.GetRequiredService<IStringLocalizer<IdentityMessages>>();
            var logger = services.GetRequiredService<ILogger<IExternalLoginsHandler>>();
            var handler = services
                .GetRequiredService<IPageHandlerProvider<ExternalLoginsModel, IExternalLoginsHandler>>().Handler;

            if (!handler.UsePage)
            {
                return Results.LocalRedirect("/Account/Manage/ExternalLogins");
            }

            var user = await handler.FetchUser(httpContext.User);
            try
            {
                if (!user.UserExists)
                {
                    logger.LogError("External login callback for an unknown user with ID '{UserId}'.",
                        handler.GetUserId(httpContext.User));
                    return Results.LocalRedirect(WithStatus("/Account/Manage/ExternalLogins",
                        localizer["Unable to load user with ID '{0}'.", handler.GetUserId(httpContext.User)].Value));
                }

                var result = await handler.AddExternalLogin(user);
                if (!result.Success && result.IdentityResult == null)
                {
                    // Frueher eine InvalidOperationException, die als Fehlerseite endete. Ein misslungener
                    // Rueckweg ist aber ein normaler Ausgang - der Benutzer soll auf seiner Liste landen und
                    // lesen, dass es nicht geklappt hat; die Ursache gehoert ins Log, nicht auf den Schirm.
                    logger.LogError(
                        "Unexpected error loading external login info for user with ID '{UserId}'.", result.UserId);
                    return Results.LocalRedirect(WithStatus("/Account/Manage/ExternalLogins",
                        localizer["The external login was not added. External logins can only be associated with one account."].Value));
                }

                if (!result.Success)
                {
                    return Results.LocalRedirect(WithStatus("/Account/Manage/ExternalLogins",
                        localizer["The external login was not added. External logins can only be associated with one account."].Value));
                }

                await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return Results.LocalRedirect(WithStatus("/Account/Manage/ExternalLogins",
                    localizer["The external login was added."].Value));
            }
            finally
            {
                handler.ReleaseUser(user);
            }
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

    /// <summary>
    /// Nimmt die uebergebene Rueckkehr-Adresse nur an, wenn sie eine seiten-eigene ist. Die Pruefung sitzt
    /// zusaetzlich zu <c>Results.LocalRedirect</c> hier, weil dessen Schutz eine Exception ist: der Benutzer saehe
    /// einen Fehler statt der Seite, auf die er gehoert.
    /// </summary>
    private static string LocalOr(string? returnUrl, string fallback)
        => !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
            ? returnUrl
            : fallback;

    /// <summary>Haengt die Statusmeldung an, die die Zielseite als <c>status</c>-Parameter anzeigt.</summary>
    private static string WithStatus(string path, string status)
        => $"{path}?status={Uri.EscapeDataString(status)}";
}
