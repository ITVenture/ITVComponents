using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

public class ExternalOAuthServiceViewModel
{
    public int OAuthServiceId { get; set; }

    [Required, MaxLength(255)]
    public string UniqueConnectionName { get; set; } = string.Empty;

    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string RevocationEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Plain-text only when the user prefixes with "encrypt:". Stored encrypted; never returned to the UI.
    /// </summary>
    public string? ClientSecret { get; set; }

    public string Scope { get; set; } = string.Empty;

    public bool Global { get; set; }

    public ExternalServiceAuthenticationType AuthenticationType { get; set; }

    public int? TenantId { get; set; }
}

public sealed class ExternalOAuthServiceTenantLoginViewModel
{
    public int ExternalOAuthServiceTenantLoginId { get; set; }
    public int TenantId { get; set; }
    public string? TenantName { get; set; }
    public int OAuthServiceId { get; set; }
    public bool Revoked { get; set; }
}
