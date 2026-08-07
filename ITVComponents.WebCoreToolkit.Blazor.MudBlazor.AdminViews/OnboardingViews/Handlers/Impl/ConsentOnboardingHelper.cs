using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Consent;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Der eine Ort, an dem der Zustimmungs-Nachweis einer Tenant-Anlage abgelegt wird - gerufen von beiden
/// Strategien, damit im flachen und im hierarchischen Betrieb dasselbe im Nachweis steht.
/// </summary>
internal static class ConsentOnboardingHelper
{
    /// <summary>
    /// Legt die Nachweise NACH der Anlage ab: vorher gibt es die Mandanten-Nummer nicht, an der sie
    /// haengen.
    /// </summary>
    /// <remarks>
    /// Die Antworten kommen aus dem Ansichtsmodell und tragen Zeitpunkt, Sprache und Stand des Dokuments
    /// bereits mit sich - sie werden hier NICHT neu bestimmt. Bei einem geparkten Onboarding liegt
    /// zwischen der Zustimmung und dieser Zeile die Mailbestaetigung, also Stunden bis Tage.
    /// <para>
    /// Ohne konfigurierte Zustimmungspunkte ist die Liste leer und es passiert nichts - dann gab es nur
    /// den eingebauten Schalter, und zu dem gibt es keinen Punkt, auf den sich ein Nachweis beziehen
    /// koennte.
    /// </para>
    /// </remarks>
    public static async Task RecordAsync(IConsentProvider provider, BillingProfileViewModel input,
        int tenantId, string? userId, string? email, ILogger? logger, CancellationToken ct)
    {
        if (input?.ConsentAnswers is not { Count: > 0 })
        {
            return;
        }

        int written = await provider.RecordAsync(input.ConsentAnswers, new ConsentSubject
        {
            UserId = userId,
            Email = email,
            TenantId = tenantId,
            Origin = ConsentOccasionKind.TenantOnboarding
        }, ct);

        if (written == 0)
        {
            // Der Provider hat den Grund bereits protokolliert; diese Zeile stellt den Bezug zum Mandanten
            // her, damit sich ein Tenant ohne dokumentierte Zustimmung wiederfinden laesst.
            logger?.LogError("Zu Tenant {TenantId} wurde zugestimmt, aber es konnte kein Nachweis abgelegt werden.", tenantId);
        }
    }
}
