namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews
{
    /// <summary>
    /// Marker type for the customer-facing onboarding views' string resources. Components inject
    /// <c>IStringLocalizer&lt;OnboardingMessages&gt;</c>; resolved against the embedded resx set
    /// (<c>Resources/OnboardingMessages.{culture}.resx</c>, neutral = English) plus DB overrides.
    /// Lives in the package root namespace so the resx manifest name lines up with <c>ResourcesPath="Resources"</c>.
    /// </summary>
    public sealed class OnboardingMessages
    {
    }
}
