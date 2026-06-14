namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.OnboardingViews.Options;

/// <summary>
/// Host configuration for the sub-tenant invitation form on the Invitations page. Read through
/// <c>IHierarchySettings&lt;TenantInvitationSettings&gt;</c>, so the values can be supplied per tenant (scoped)
/// or globally from the database (requires <c>UseHierarchySettings()</c> + activated scoped/global settings on
/// the host) and fall back to a default instance when not configured.
/// <para>
/// <see cref="AdminRole"/> / <see cref="Template"/> are the recommended admin role and tenant template applied
/// to a sub-tenant invitation. The matching <c>Allow…Override</c> flag decides whether the inviting user may
/// deviate from that recommendation: when false the corresponding input is hidden and the configured value is
/// applied silently; when true the inviter sees an editable field pre-filled with the recommendation. The
/// values only concern sub-tenant invitations — employee invitations carry no role/template.
/// </para>
/// </summary>
public class TenantInvitationSettings
{
    /// <summary>Recommended admin role assigned when the invited sub-tenant is created.</summary>
    public string? AdminRole { get; set; }

    /// <summary>Recommended tenant template applied when the invited sub-tenant is created.</summary>
    public string? Template { get; set; }

    /// <summary>When true, the inviter may override <see cref="AdminRole"/>; otherwise it is applied silently.</summary>
    public bool AllowRoleOverride { get; set; }

    /// <summary>When true, the inviter may override <see cref="Template"/>; otherwise it is applied silently.</summary>
    public bool AllowTemplateOverride { get; set; }
}
