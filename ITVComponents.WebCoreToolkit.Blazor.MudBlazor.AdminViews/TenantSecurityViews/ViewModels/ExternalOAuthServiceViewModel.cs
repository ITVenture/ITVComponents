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

    /// <summary>
    /// Tenants without own (or inherited) credentials fall back to the platform's credentials. Only meaningful for a global
    /// service; only a sysadmin sets it.
    /// </summary>
    public bool AllowPlatformCredentials { get; set; }

    /// <summary>
    /// Sub-tenants see this service (tenant tree only, never for a global service). Only the definition - whether they use
    /// the account as well is decided on the credentials row.
    /// </summary>
    public bool Inheritable { get; set; }
}

/// <summary>One credentials row of a service (formerly "tenant login"): token and/or fixed credentials of one owner.</summary>
public sealed class ExternalOAuthServiceTenantLoginViewModel
{
    public int ExternalOAuthServiceTenantLoginId { get; set; }

    /// <summary>The row of the platform (no tenant).</summary>
    public bool IsPlatform => TenantId == null;

    /// <summary>Whether fixed credentials (user/secret) are stored - never what they are.</summary>
    public bool HasCredentials { get; set; }

    /// <summary>Whether a token is stored.</summary>
    public bool HasToken { get; set; }

    /// <summary>Whether descendants in the tenant tree without an own row use this one.</summary>
    public bool Inheritable { get; set; }

    /// <summary>Whether the current user may change this row (the own tenant's row; a sysadmin every row).</summary>
    public bool Editable { get; set; }
    /// <summary>Der Mandant der Zugangsdaten; null = die Plattform.</summary>
    public int? TenantId { get; set; }
    public string? TenantName { get; set; }
    public int OAuthServiceId { get; set; }
    public bool Revoked { get; set; }
}

/// <summary>What the credentials dialog sends: the fixed credentials of one owner for one service.</summary>
public sealed class ExternalServiceCredentialInputViewModel
{
    public int OAuthServiceId { get; set; }

    /// <summary>The platform row instead of the own tenant's (sysadmin, global service only).</summary>
    public bool Platform { get; set; }

    /// <summary>User or client of the account (Basic, password grant, client credentials); ignored for the other types.</summary>
    public string? User { get; set; }

    /// <summary>The secret; empty keeps the stored one.</summary>
    public string? Secret { get; set; }

    /// <summary>Pass on to descendants in the tenant tree.</summary>
    public bool Inheritable { get; set; }
}

public sealed class ExternalServiceCredentialSaveResult
{
    public bool Success { get; private init; }

    public string? Error { get; private init; }

    public static ExternalServiceCredentialSaveResult Saved() => new() { Success = true };

    public static ExternalServiceCredentialSaveResult Failed(string error) => new() { Error = error };
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
    public KeyValuePair<string, string>[] TokenRequestParameters { get; set; } = Array.Empty<KeyValuePair<string, string>>();
}

public sealed class ExternalOAuthServiceParameterViewModel
{
    public int OAuthServiceParameterId { get; set; }

    public int OAuthServiceId { get; set; }

    [Required, MaxLength(255)]
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>The value; for a secret parameter always null when read (it is never shown again), and empty on save means "keep".</summary>
    public string? ParameterValue { get; set; }

    /// <summary>Whether the value is stored encrypted.</summary>
    public bool IsSecret { get; set; }

    /// <summary>Whether a value is stored - the only thing the view learns about a secret.</summary>
    public bool HasValue { get; set; }
}

public sealed class ExternalOAuthServiceParameterSaveResult
{
    public bool Success { get; private init; }

    /// <summary>Why the parameter was rejected (reserved name, duplicate, not editable)</summary>
    public string? Error { get; private init; }

    public ExternalOAuthServiceParameterViewModel? Parameter { get; private init; }

    public static ExternalOAuthServiceParameterSaveResult Saved(ExternalOAuthServiceParameterViewModel parameter)
        => new() { Success = true, Parameter = parameter };

    public static ExternalOAuthServiceParameterSaveResult Failed(string error)
        => new() { Success = false, Error = error };
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
