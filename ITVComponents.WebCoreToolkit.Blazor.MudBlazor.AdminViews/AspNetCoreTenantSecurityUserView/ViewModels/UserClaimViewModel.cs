using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.ViewModels;

public class UserClaimViewModel
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";

    [Required]
    public string ClaimType { get; set; } = "";

    public string? ClaimValue { get; set; }
}
