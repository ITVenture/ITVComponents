using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IAuthenticationTypeAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<AuthenticationTypeViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<AuthenticationTypeViewModel?> CreateAsync(ClaimsPrincipal user, AuthenticationTypeViewModel input);
    Task<AuthenticationTypeViewModel?> UpdateAsync(ClaimsPrincipal user, AuthenticationTypeViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int authenticationTypeId);

    Task<PagedResult<AuthenticationClaimMappingViewModel>> ListClaimsAsync(ClaimsPrincipal user, int authenticationTypeId, ListQuery query);
    Task<AuthenticationClaimMappingViewModel?> CreateClaimAsync(ClaimsPrincipal user, int authenticationTypeId, AuthenticationClaimMappingViewModel input);
    Task<AuthenticationClaimMappingViewModel?> UpdateClaimAsync(ClaimsPrincipal user, AuthenticationClaimMappingViewModel input);
    Task<bool> DeleteClaimAsync(ClaimsPrincipal user, int claimMappingId);
}
