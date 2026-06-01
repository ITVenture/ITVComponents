using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class AuthenticationTypeViewModel
{
    [Key]
    public int AuthenticationTypeId { get; set; }

    [Required, MaxLength(512)]
    public string AuthenticationTypeName { get; set; } = "";
}

public class AuthenticationClaimMappingViewModel
{
    [Key]
    public int AuthenticationClaimMappingId { get; set; }

    public int AuthenticationTypeId { get; set; }

    [MaxLength(512), Required]
    public string IncomingClaimName { get; set; } = "";

    public string? Condition { get; set; }

    [MaxLength(512), Required]
    public string OutgoingClaimName { get; set; } = "";

    [MaxLength(512)]
    public string? OutgoingValueType { get; set; }

    [MaxLength(512)]
    public string? OutgoingIssuer { get; set; }

    [MaxLength(512)]
    public string? OutgoingOriginalIssuer { get; set; }

    [Required]
    public string OutgoingClaimValue { get; set; } = "";
}
