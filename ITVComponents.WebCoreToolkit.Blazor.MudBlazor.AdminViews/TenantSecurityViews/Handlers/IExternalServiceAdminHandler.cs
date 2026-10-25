using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IExternalServiceAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<ExternalOAuthServiceViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<ExternalOAuthServiceViewModel?> CreateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input);
    Task<ExternalOAuthServiceViewModel?> UpdateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int oauthServiceId);

    Task<PagedResult<ExternalOAuthServiceTenantLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, int oauthServiceId, ListQuery query);
    Task<bool> RevokeLoginAsync(ClaimsPrincipal user, int externalOAuthServiceTenantLoginId);

    Task<ExternalServiceDetailsViewModel?> GetDetailsAsync(ClaimsPrincipal user, int oauthServiceId);
    Task<ExternalServiceTestResultViewModel> PerformTestAsync(ClaimsPrincipal user, ExternalServiceTestRequestViewModel request);

    /// <summary>Additional form-parameters that are sent with the token-request of the service</summary>
    Task<PagedResult<ExternalOAuthServiceParameterViewModel>> ListParametersAsync(ClaimsPrincipal user, int oauthServiceId, ListQuery query);

    /// <summary>Creates (id 0) or updates a token-parameter; a rejected parameter comes back with the reason</summary>
    Task<ExternalOAuthServiceParameterSaveResult> SaveParameterAsync(ClaimsPrincipal user, ExternalOAuthServiceParameterViewModel input);

    Task<bool> DeleteParameterAsync(ClaimsPrincipal user, int oauthServiceId, int oauthServiceParameterId);
}
