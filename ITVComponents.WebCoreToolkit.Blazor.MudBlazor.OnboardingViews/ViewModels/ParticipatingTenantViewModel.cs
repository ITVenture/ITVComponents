using ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models;

namespace ITVComponents.WebCoreToolkit.OnboardingViews.Blazor.ViewModels;

/// <summary>
/// Read-only projection of a tenant that the current user participates in (either as owner or
/// invited employee). Rendered as a row in the <c>MyTenants</c> grid.
/// </summary>
public class ParticipatingTenantViewModel
{
    public int BillingProfileId { get; set; }

    public ProfileType ProfileType { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? DefaultEmail { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public InvitationStatus Status { get; set; }

    public bool TenantCreated { get; set; }
}
