// The assembly name (…Blazor.MudBlazor.BillingViews) differs from the root namespace (…BillingViews.Blazor).
// Without this attribute the localizer factory derives the resource base name from the ASSEMBLY name and never
// finds the embedded resx (it then returns the resource key verbatim, e.g. "Plans_Title"). Declaring the root
// namespace explicitly makes GetRootNamespace() resolve the base name to …BillingViews.Blazor.Resources.* which
// matches the embedded manifest names.
[assembly: Microsoft.Extensions.Localization.RootNamespace("ITVComponents.WebCoreToolkit.BillingViews.Blazor")]

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor
{
    /// <summary>
    /// Marker type for the billing-views string resources. Components inject
    /// <c>IStringLocalizer&lt;BillingMessages&gt;</c>; the toolkit's localizer factory resolves the embedded
    /// resx set (<c>Resources/BillingMessages.{culture}.resx</c>, neutral = English) plus DB overrides.
    /// </summary>
    public sealed class BillingMessages
    {
    }
}
