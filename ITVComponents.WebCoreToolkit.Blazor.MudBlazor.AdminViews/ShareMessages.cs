namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews
{
    /// <summary>
    /// Marker type for the string resources of the sharing views (share button, share dialog, link dialog).
    /// Components inject <c>IStringLocalizer&lt;ShareMessages&gt;</c>; resolved against the embedded resx set
    /// (<c>Resources/ShareMessages.{culture}.resx</c>, neutral = English) plus DB overrides.
    /// Lives in the package root namespace so the resx manifest name lines up with <c>ResourcesPath="Resources"</c>.
    /// </summary>
    /// <remarks>
    /// Geteilt wird auf den Seiten der Anwendung, nicht in der Administration - wer den Knopf drueckt, ist
    /// ein gewoehnlicher Benutzer. Deshalb sind diese Masken uebersetzt.
    /// </remarks>
    public sealed class ShareMessages
    {
    }
}
