namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews
{
    /// <summary>
    /// Marker type for the string resources of the client-application views. Components inject
    /// <c>IStringLocalizer&lt;ClientAppMessages&gt;</c>; resolved against the embedded resx set
    /// (<c>Resources/ClientAppMessages.{culture}.resx</c>, neutral = English) plus DB overrides.
    /// Lives in the package root namespace so the resx manifest name lines up with <c>ResourcesPath="Resources"</c>.
    /// </summary>
    /// <remarks>
    /// Diese Masken bedient der <b>Kunde</b>, nicht der System-Administrator: er legt seine eigenen
    /// Anwendungen an, gesteht ihnen Berechtigungs-Sets zu und bestaetigt die Kopplung seiner Geraete.
    /// Deshalb sind sie uebersetzt - und deshalb haengen sie am Feature <c>ITVCustomerViews</c> statt an
    /// <c>ITVAdminViews</c>.
    /// </remarks>
    public sealed class ClientAppMessages
    {
    }
}
