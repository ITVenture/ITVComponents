using System.Globalization;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Handlers.Impl;

/// <summary>
/// Bereitet Sprache und Zeitzone aus der Profil-Erfassung für den Tenant auf.
/// </summary>
/// <remarks>
/// <para>
/// Leer bleibt leer (<c>null</c>) - das heisst "geerbt", und das ist ein Normalfall.
/// </para>
/// <para>
/// Ein Wert, der sich nicht lesen lässt, wird <b>nicht</b> gespeichert: eine unbekannte Zeitzone würde später
/// bei JEDER Zeitberechnung dieses Mandanten werfen, und ein Anlegen soll daran nicht scheitern. Die Maske
/// lässt nur gültige Werte zu; kommt trotzdem einer an (geparktes Onboarding aus einem anderen System,
/// manipulierte Anfrage), steht das im Log.
/// </para>
/// </remarks>
internal static class TenantLocaleInput
{
    /// <summary>Die Zeitzone, wie sie gespeichert wird, oder <c>null</c>.</summary>
    public static string? TimeZone(string? input, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var id = input.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out _))
        {
            return id;
        }

        logger.LogWarning("Onboarding: the time zone '{TimeZone}' is unknown on this server; the new tenant inherits its time zone instead.", id);
        return null;
    }

    /// <summary>
    /// Die Kultur, wie gewählt (<c>de-CH</c> bleibt <c>de-CH</c>), in ihrer üblichen Schreibweise, oder <c>null</c>.
    /// </summary>
    public static string? Culture(string? input, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(input.Trim());
            if (string.IsNullOrEmpty(culture.Name))
            {
                logger.LogWarning("Onboarding: the invariant culture is not a tenant language; the new tenant inherits its language instead.");
                return null;
            }

            return culture.Name;
        }
        catch (CultureNotFoundException ex)
        {
            logger.LogWarning(ex, "Onboarding: the language '{Culture}' is not a valid culture; the new tenant inherits its language instead.", input);
            return null;
        }
    }
}
