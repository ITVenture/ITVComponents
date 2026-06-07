using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IExternalServiceAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<ExternalOAuthServiceViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<ExternalOAuthServiceViewModel?> CreateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input);
    Task<ExternalOAuthServiceViewModel?> UpdateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int oauthServiceId);

    Task<PagedResult<ExternalOAuthServiceTenantLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, int oauthServiceId, ListQuery query);
    Task<bool> RevokeLoginAsync(ClaimsPrincipal user, int externalOAuthServiceTenantLoginId);

    Task<ExternalServiceDetailsViewModel?> GetDetailsAsync(ClaimsPrincipal user, int oauthServiceId);
    Task<ExternalServiceTestResultViewModel> PerformTestAsync(ClaimsPrincipal user, ExternalServiceTestRequestViewModel request);
}
