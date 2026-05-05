using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.ViewModels;

public class UserViewModel
{
    public string? Id { get; set; }

    [MaxLength(150), Required]
    public string UserName { get; set; } = "";

    public int? AuthenticationTypeId { get; set; }

    public int? TenantId { get; set; }

    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }

    public bool Enabled { get; set; }
}
