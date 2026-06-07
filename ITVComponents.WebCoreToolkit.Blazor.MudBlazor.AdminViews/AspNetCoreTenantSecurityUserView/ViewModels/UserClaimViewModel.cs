using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.AspNetCoreTenantSecurityUserView.ViewModels;

public class UserClaimViewModel
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";

    [Required]
    public string ClaimType { get; set; } = "";

    public string? ClaimValue { get; set; }
}
