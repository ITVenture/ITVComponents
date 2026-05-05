using System.Security.Claims;
using ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.AspNetCoreTenantSecurityUserView.Blazor.Handlers;

public interface IUserAdminHandler
{
    UserListContext GetContext(ClaimsPrincipal user);

    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<UserViewModel>> ListUsersAsync(ClaimsPrincipal user, UserListQuery query);

    Task<UserViewModel?> CreateUserAsync(ClaimsPrincipal user, UserViewModel input);

    Task<UserViewModel?> UpdateUserAsync(ClaimsPrincipal user, UserViewModel input);

    Task<bool> DeleteUserAsync(ClaimsPrincipal user, string userOrTenantUserId, int? tenantId);

    Task<PagedResult<CustomUserPropertyViewModel>> ListPropertiesAsync(ClaimsPrincipal user, string userId, UserListQuery query);
    Task<CustomUserPropertyViewModel?> CreatePropertyAsync(ClaimsPrincipal user, string userId, CustomUserPropertyViewModel input);
    Task<CustomUserPropertyViewModel?> UpdatePropertyAsync(ClaimsPrincipal user, CustomUserPropertyViewModel input);
    Task<bool> DeletePropertyAsync(ClaimsPrincipal user, int customUserPropertyId);

    Task<PagedResult<UserLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, string userId, UserListQuery query);
    Task<bool> DeleteLoginAsync(ClaimsPrincipal user, UserLoginViewModel input);

    Task<PagedResult<UserTokenViewModel>> ListTokensAsync(ClaimsPrincipal user, string userId, UserListQuery query);
    Task<bool> DeleteTokenAsync(ClaimsPrincipal user, UserTokenViewModel input);

    Task<PagedResult<UserClaimViewModel>> ListClaimsAsync(ClaimsPrincipal user, string userId, UserListQuery query);
    Task<UserClaimViewModel?> CreateClaimAsync(ClaimsPrincipal user, string userId, UserClaimViewModel input);
    Task<UserClaimViewModel?> UpdateClaimAsync(ClaimsPrincipal user, UserClaimViewModel input);
    Task<bool> DeleteClaimAsync(ClaimsPrincipal user, int claimId);
}
