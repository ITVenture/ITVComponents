using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

/// <summary>
/// Was sich an einer bestehenden Freigabe noch aendern laesst.
/// <para>
/// <b>Nicht dabei: worauf sie zeigt.</b> Die Argumentwerte stehen fest, seit die Freigabe entstanden
/// ist - sie nachtraeglich umzubiegen hiesse, einen verschickten Link stillschweigend auf ein anderes
/// Objekt zu richten. Wer etwas anderes teilen will, teilt etwas anderes.
/// </para>
/// </summary>
public class SharedAssetEditViewModel
{
    public string AssetKey { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string AssetTitle { get; set; } = string.Empty;

    /// <summary>Ab wann sie gilt. Leer = sofort.</summary>
    public DateTime? NotBefore { get; set; }

    /// <summary>Bis wann sie gilt. Leer = unbegrenzt.</summary>
    public DateTime? NotAfter { get; set; }

    /// <summary>An wen sie gerichtet ist. Eine Behauptung, kein Nachweis.</summary>
    [MaxLength(256)]
    public string? RecipientLabel { get; set; }

    /// <summary>
    /// Wer sie benutzen darf - Benutzerkennungen, dazu die Platzhalter <c>%</c> (alle) und
    /// <c>##ANONYMOUS</c> (ohne Anmeldung).
    /// </summary>
    public List<string> UserFilters { get; set; } = new();

    /// <summary>
    /// Welche Mandanten sie benutzen duerfen, dazu <c>%</c>.
    /// </summary>
    public List<string> TenantFilters { get; set; } = new();

    /// <summary>Worauf sie zeigt - nur zur Anzeige.</summary>
    public string? ArgumentSummary { get; set; }
}
