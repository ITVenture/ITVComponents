using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

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

public sealed class ExternalServiceDetailsViewModel
{
    public int OAuthServiceId { get; set; }
    public string UniqueConnectionName { get; set; } = string.Empty;
    public string GlobalUniqueConnectionName { get; set; } = string.Empty;
    public ExternalServiceAuthenticationType AuthenticationType { get; set; }
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? RevocationEndpoint { get; set; }
    public string? ClientId { get; set; }
    public string? Scope { get; set; }
    public bool Global { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}

public sealed class ExternalServiceTestRequestViewModel
{
    public int OAuthServiceId { get; set; }

    [Required]
    public string Verb { get; set; } = "GET";

    [Required]
    public string TargetUrl { get; set; } = string.Empty;

    public string? HttpActionBody { get; set; }

    public string ActionBodyContentType { get; set; } = "application/json";

    public string? CustomHeaders { get; set; }
}

public sealed class ExternalServiceTestResultViewModel
{
    public int StatusCode { get; set; }
    public string? Content { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
