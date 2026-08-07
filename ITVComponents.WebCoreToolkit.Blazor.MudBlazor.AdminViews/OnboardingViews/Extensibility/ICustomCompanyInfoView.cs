using System.Text.Json.Nodes;
using ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Extensibility;

/// <summary>
/// Der Vertrag jeder eigenen Zusatzangaben-Maske: sie zeigt an und liefert auf Zuruf ihren Datensatz.
/// Mehr nicht.
/// </summary>
/// <remarks>
/// Der Rahmen gehoert der Erfassung: Reiter, Beschriftung, Absende-Knopf, die Pruefung ueber alle Reiter
/// hinweg und das Anspringen des Reiters, an dem etwas fehlt. Fuer jede Maske gleich. Eine Maske, die
/// ihren eigenen Absende-Knopf mitbraechte, laege damit im Inhalt statt in der Fusszeile des Formulars -
/// und der Benutzer haette zwei Knoepfe, von denen nur einer den Vorgang wirklich abschliesst.
/// </remarks>
public interface ICustomCompanyInfoView
{
    /// <summary>
    /// Das Formular wird abgeschickt: die Maske prueft ihre Eingaben und liefert ihren Datensatz - oder
    /// sagt, dass noch etwas fehlt.
    /// </summary>
    Task<CustomInfoViewResult> ResolveValuesAsync();
}

/// <summary>
/// Die Antwort einer Maske aufs Abschicken: entweder der Datensatz, oder die Auskunft, dass noch etwas
/// fehlt.
/// </summary>
/// <remarks>
/// Bewusst kein blosses <c>JsonNode?</c> mit "null heisst nicht bereit": null ist ein gueltiger Datensatz
/// (eine Maske, die nur etwas anzeigt, hat keinen). Die beiden Faelle muessen unterscheidbar bleiben,
/// sonst schluepft ein unvollstaendiges Formular als "ausgefuellt" durch.
/// </remarks>
public sealed class CustomInfoViewResult
{
    private CustomInfoViewResult(bool canComplete, JsonNode? values, string? message)
    {
        CanComplete = canComplete;
        Values = values;
        Message = message;
    }

    /// <summary>Darf der Vorgang mit diesem Stand weitergehen?</summary>
    public bool CanComplete { get; }

    /// <summary>Der Datensatz der Maske - er landet unveraendert im Vorgang.</summary>
    public JsonNode? Values { get; }

    /// <summary>
    /// Optionale Meldung, wenn noch etwas fehlt. Null, wenn die Maske die fehlenden Stellen bereits
    /// selbst markiert hat - dann waere eine zusaetzliche Einblendung nur Laerm.
    /// </summary>
    public string? Message { get; }

    /// <summary>Alles beisammen.</summary>
    public static CustomInfoViewResult Complete(JsonNode? values = null)
        => new CustomInfoViewResult(true, values, null);

    /// <summary>Noch nicht so weit - der Vorgang bleibt offen.</summary>
    public static CustomInfoViewResult Incomplete(string? message = null)
        => new CustomInfoViewResult(false, null, message);
}

/// <summary>
/// Was eine eigene Maske ueber ihren Platz im Vorgang wissen muss. Kommt als CascadingParameter herein.
/// </summary>
public sealed class CustomCompanyInfoViewContext
{
    public CustomCompanyInfoViewContext(string handlerKey, CustomInfoContext info, JsonNode? existing,
        Func<string?, string?> translate)
    {
        HandlerKey = handlerKey;
        Info = info;
        Existing = existing;
        Translate = translate;
    }

    /// <summary>Der Schluessel des Moduls, zu dem diese Maske gehoert.</summary>
    public string HandlerKey { get; }

    /// <summary>Der Erfassungsfall - Anlage oder Nachtrag, selbst oder auf Einladung.</summary>
    public CustomInfoContext Info { get; }

    /// <summary>
    /// Der bereits abgelegte Datensatz zur Vorbelegung, oder null. Beim Anlegen immer null.
    /// </summary>
    public JsonNode? Existing { get; }

    /// <summary>
    /// Loest Klartext oder Kultur-JSON in die Kultur des Lesers auf. Eine eigene Maske bringt ihre Texte
    /// in der Regel selbst mit - fuer Werte aus der Konfiguration steht dieser Weg trotzdem offen.
    /// </summary>
    public Func<string?, string?> Translate { get; }
}
