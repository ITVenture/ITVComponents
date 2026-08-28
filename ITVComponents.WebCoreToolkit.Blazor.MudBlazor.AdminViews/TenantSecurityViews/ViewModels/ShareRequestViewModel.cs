using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

/// <summary>
/// Was der Teilende angibt.
/// </summary>
public class ShareRequestViewModel
{
    /// <summary>Der Pfad, auf dem geteilt wird - er entscheidet, welche Vorlagen ueberhaupt passen.</summary>
    public string RequestPath { get; set; } = string.Empty;

    [Required]
    public string TemplateKey { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Die Werte der Argumente dieser Vorlage. Aus dem laufenden Kontext vorbelegt - das ist der
    /// eigentliche Nutzen der Konsumenten-Registry.
    /// </summary>
    public Dictionary<string, string> ArgumentValues { get; set; } = new();

    /// <summary>
    /// An wen die Freigabe gerichtet ist. Eine Behauptung, kein Nachweis.
    /// </summary>
    [MaxLength(256)]
    public string? RecipientLabel { get; set; }

    /// <summary>
    /// Ob ein Link entstehen soll, der ohne Anmeldung funktioniert.
    /// </summary>
    public bool Anonymous { get; set; }

    /// <summary>
    /// Ob die Freigabe <b>nirgends gespeichert</b> wird, sondern verschluesselt in der URL reist.
    /// <para>
    /// Das ist der schmale Fall - ein Objekt, ein Empfaenger, kurze Frist. Der Preis: ein solches
    /// Ticket laesst sich nicht einzeln zurueckziehen, sondern endet von selbst.
    /// </para>
    /// </summary>
    public bool AdHoc { get; set; }

    /// <summary>
    /// Wie lange ein Ad-hoc-Ticket gilt. Leer = die Hoechstdauer der Vorlage, die auch die Obergrenze
    /// ist.
    /// </summary>
    public int? LifetimeMinutes { get; set; }

    /// <summary>
    /// Wen die Freigabe erreichen soll. <b>Pflichtangabe</b>, und ohne Vorbelegung: eine Vorgabe waere
    /// hier eine unausgesprochene Entscheidung darueber, wer an fremde Daten kommt.
    /// <para>
    /// Ohne Bedeutung, wenn die Freigabe ohne Anmeldung funktionieren soll (dann ist das die Reichweite)
    /// oder wenn sie ad hoc ist (die reist im Link und wird nirgends gespeichert).
    /// </para>
    /// </summary>
    public ShareReach? Reach { get; set; }

    /// <summary>Die Benutzerkennungen bei <see cref="ShareReach.Recipients"/>.</summary>
    public List<string> UserFilters { get; set; } = new();

    /// <summary>Die Mandanten bei <see cref="ShareReach.Tenants"/>.</summary>
    public List<string> TenantFilters { get; set; } = new();
}

/// <summary>
/// Wen eine gespeicherte Freigabe erreicht. Geprueft wird gegen den AUFRUFER - der Mandant der Freigabe
/// selbst steht woanders und entscheidet nicht mit, sondern ist die Wirkung: wer hereinkommt, arbeitet
/// fuer diese Anfrage im Mandanten der Freigabe.
/// </summary>
public enum ShareReach
{
    /// <summary>Jeder Angemeldete, der den Link hat - unabhaengig von seinen eigenen Mandanten.</summary>
    AnyoneSignedIn,

    /// <summary>Nur diese Benutzerkennungen.</summary>
    Recipients,

    /// <summary>Nur Mitglieder dieser Mandanten.</summary>
    Tenants
}

/// <summary>
/// Was dabei herauskommt: ein Link - oder eine Meldung, die sagt, was fehlt.
/// </summary>
public class ShareResultViewModel
{
    public bool Success { get; set; }

    public string? Link { get; set; }

    public string? AssetKey { get; set; }

    /// <summary>
    /// Warum nichts entstanden ist. Kommt aus der harten Pruefung und benennt das fehlende oder
    /// unpassende Argument - eine gemeinsame Meldung "geht nicht" waere im Support wertlos.
    /// </summary>
    public string? Error { get; set; }
}
