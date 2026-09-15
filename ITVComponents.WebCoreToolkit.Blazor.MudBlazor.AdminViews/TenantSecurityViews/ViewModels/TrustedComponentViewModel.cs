using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public class TrustedComponentViewModel
{
    public int TrustedFullAccessComponentId { get; set; }

    [Required, MaxLength(1024)]
    public string FullQualifiedTypeName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? TargetQualifiedTypeName { get; set; }

    public string? Description { get; set; }

    public string? TrustLevelConfig { get; set; }

    /// <summary>
    /// Ob dieser Eintrag zur Laufzeit ueberhaupt treffen kann.
    /// </summary>
    /// <remarks>
    /// Wird beim Auflisten gegen die geladenen Assemblies ermittelt und nicht gespeichert. Ein Eintrag, der
    /// nicht mehr aufloest, bricht <b>nichts Sichtbares</b> - er entzieht nur stillschweigend die besonderen
    /// Rechte. Deshalb steht er hier als Spalte.
    /// </remarks>
    public bool Resolvable { get; set; } = true;

    /// <summary>
    /// Im Klartext, warum der Eintrag nicht trifft - <c>null</c>, wenn er trifft.
    /// </summary>
    public string? ResolveHint { get; set; }
}
