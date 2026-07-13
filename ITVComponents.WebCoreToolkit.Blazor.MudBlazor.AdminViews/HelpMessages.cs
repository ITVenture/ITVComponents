namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews
{
    /// <summary>
    /// Marker type for the public help viewer's string resources (chrome only; the help content itself is
    /// localized per topic in the database). Components inject <c>IStringLocalizer&lt;HelpMessages&gt;</c>;
    /// resolved against the embedded resx set (<c>Resources/HelpMessages.{culture}.resx</c>, neutral = English)
    /// plus DB overrides. Lives in the package root namespace so the resx manifest name lines up with
    /// <c>ResourcesPath="Resources"</c>.
    /// </summary>
    public sealed class HelpMessages
    {
    }
}
