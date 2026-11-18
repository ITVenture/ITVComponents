namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.UpdateServiceViews
{
    /// <summary>
    /// Marker type for the texts of the tab "Updates" in the detail row of the client apps. Components inject
    /// <c>IStringLocalizer&lt;UpdateServiceMessages&gt;</c>; resolved against the embedded
    /// <c>Resources/UpdateServiceMessages.{culture}.resx</c> (neutral = English).
    /// </summary>
    /// <remarks>
    /// Localized like the other client app masks: there the <b>customer</b> sets up the tills. The page of the releases is
    /// a platform page and stays English, like the other platform pages of the admin views.
    /// </remarks>
    public sealed class UpdateServiceMessages
    {
    }
}
