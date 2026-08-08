using System.Security.Claims;
using System.Text.Json.Nodes;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Der eine Ort, an dem festgelegt ist, WANN die Zusatzangaben-Module gefragt werden. Beide Strategien
/// rufen ihn - haetten sie jede ihre eigene Reihenfolge, koennten sich Module im flachen und im
/// hierarchischen Betrieb unterschiedlich verhalten, ohne dass es jemand beabsichtigt haette.
/// </summary>
internal static class CustomCompanyInfoOnboardingHelper
{
    /// <summary>
    /// Baut den Erfassungsfall aus dem Ansichtsmodell. Die Mandanten-Strategie kommt darin bewusst nicht
    /// vor: <c>ParentTenantId</c> ist im flachen Betrieb schlicht null.
    /// </summary>
    public static CustomInfoContext ContextFor(BillingProfileViewModel input, CustomInfoMode mode,
        int? tenantId, ClaimsPrincipal user)
        => new CustomInfoContext
        {
            Mode = mode,
            Origin = string.IsNullOrEmpty(input.InvitationToken)
                ? CustomInfoOrigin.SelfService
                : CustomInfoOrigin.Invitation,
            ProfileType = input.ProfileType,
            TenantId = tenantId,
            ParentTenantId = input.ParentTenantId,
            User = user
        };

    /// <summary>
    /// Prueft die Zusatzangaben, bevor der Tenant entsteht. Das ist das Sicherheitsnetz hinter der
    /// Pruefung im Formular - es faengt den Weg ab, auf dem die Angaben NICHT aus einem gerade
    /// ausgefuellten Formular stammen, sondern aus einem geparkten Vorgang.
    /// </summary>
    /// <returns>true, wenn angelegt werden darf</returns>
    public static async Task<bool> AcceptsAsync(ICustomCompanyInfoProvider provider,
        BillingProfileViewModel input, CustomInfoContext ctx, ILogger logger, CancellationToken ct)
    {
        CustomInfoCheckResult check = await provider.ValidateAsync(ctx, Buckets(input), ct);
        if (check.Valid)
        {
            return true;
        }

        logger?.LogWarning("Tenant creation was rejected by the custom-company-info module '{Handler}': {Message}",
            check.HandlerKey, check.Message);
        return false;
    }

    /// <summary>
    /// Legt die Zusatzangaben ab - NACH dem Commit, weil es die TenantId vorher nicht gibt und die
    /// Module in fremde Ablagen schreiben, mit denen es keine gemeinsame Transaktion gibt.
    /// </summary>
    /// <remarks>
    /// Scheitert ein Modul, bleibt der Tenant bestehen und seine Angaben fehlen. Das ist die ehrliche
    /// Auskunft: alles andere hiesse, einen fertig angelegten Tenant wieder abzuraeumen, um Angaben
    /// willen, die der Benutzer im Firmenprofil nachtragen kann.
    /// </remarks>
    /// <returns>true, wenn alles abgelegt werden konnte</returns>
    public static async Task<bool> PersistAsync(ICustomCompanyInfoProvider provider,
        BillingProfileViewModel input, CustomInfoContext ctx, int tenantId, ILogger logger,
        CancellationToken ct)
    {
        ctx.TenantId = tenantId;
        CustomInfoPersistResult result = await provider.PersistAsync(ctx, Buckets(input), ct);
        if (result.Complete)
        {
            return true;
        }

        // Der Provider hat jeden Einzelfall schon protokolliert; diese Zeile ist die Zusammenfassung, an
        // der sich ein Tenant mit unvollstaendigen Zusatzangaben wiederfinden laesst.
        logger?.LogError(
            "The custom company info of tenant {TenantId} is incomplete - failed: [{Failed}], not supplied: [{Missing}]. It has to be added in the billing profile.",
            tenantId, string.Join(", ", result.Failed), string.Join(", ", result.Missing));
        return false;
    }

    /// <summary>
    /// Die Datensaetze aus dem Ansichtsmodell. Null-Eintraege fliegen raus: ein Modul, zu dem nichts
    /// erfasst wurde, soll als "nicht mitgeliefert" gelten und nicht als "leer uebergeben".
    /// </summary>
    private static IReadOnlyDictionary<string, JsonNode> Buckets(BillingProfileViewModel input)
    {
        var result = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, JsonNode?> entry in input.CustomInfo ?? new Dictionary<string, JsonNode?>())
        {
            if (entry.Value is not null)
            {
                result[entry.Key] = entry.Value;
            }
        }

        return result;
    }
}
