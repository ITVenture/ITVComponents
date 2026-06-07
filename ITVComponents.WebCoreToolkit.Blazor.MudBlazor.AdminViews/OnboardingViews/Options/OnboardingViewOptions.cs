using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Options;

/// <summary>
/// WebPart-level options for the OnboardingViews Blazor library. Decides which
/// strategy-specific DI-extension <c>WebPartInit</c> calls (flat = <c>ISecurityContextWithOnboarding</c>,
/// hierarchy = <c>IHierarchySecurityContextWithOnboarding</c>) based on the host's chosen DbContext.
/// </summary>
[SettingName("ContextSettings")]
public class OnboardingViewOptions
{
    /// <summary>
    /// When true, <c>WebPartInit</c> resolves <see cref="ContextType"/> and wires up the matching
    /// onboarding handler. When false, the host opts out of automatic wiring and must register
    /// the handler manually.
    /// </summary>
    public bool ConfigureContext { get; set; }

    /// <summary>
    /// Fully-qualified type name of the consumer's DbContext. Must implement either
    /// <c>ISecurityContextWithOnboarding</c> (flat) or <c>IHierarchySecurityContextWithOnboarding</c> (tree);
    /// <c>WebPartInit</c> probes both via reflection and picks the matching DI-extension.
    /// </summary>
    public string? ContextType { get; set; }
}
