using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface ITenantAdminHandler
{
    AdminContext GetContext(ClaimsPrincipal user);

    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<TenantViewModel>> ListTenantsAsync(ClaimsPrincipal user, ListQuery query);
    Task<TenantViewModel?> CreateTenantAsync(ClaimsPrincipal user, TenantViewModel input);
    Task<TenantViewModel?> UpdateTenantAsync(ClaimsPrincipal user, TenantViewModel input);
    Task<bool> DeleteTenantAsync(ClaimsPrincipal user, int tenantId);

    Task<PagedResult<TenantAssignmentViewModel>> ListTenantAssignmentsForUserAsync(
        ClaimsPrincipal user, string userId, ListQuery query);
    Task<bool> SetUserTenantAssignmentAsync(
        ClaimsPrincipal user, string userId, int tenantId, bool assigned);

    Task<PagedResult<TenantSettingViewModel>> ListSettingsAsync(ClaimsPrincipal user, int tenantId, ListQuery query);
    Task<TenantSettingViewModel?> CreateSettingAsync(ClaimsPrincipal user, int tenantId, TenantSettingViewModel input);
    Task<TenantSettingViewModel?> UpdateSettingAsync(ClaimsPrincipal user, TenantSettingViewModel input);
    Task<bool> DeleteSettingAsync(ClaimsPrincipal user, int tenantSettingId);

    Task<PagedResult<TenantFeatureActivationAssignmentViewModel>> ListFeatureActivationsForTenantAsync(
        ClaimsPrincipal user, int tenantId, ListQuery query);
    Task<bool> SetFeatureActivationForTenantAsync(
        ClaimsPrincipal user, int tenantId, int featureId, bool assigned, DateTime? activationStart, DateTime? activationEnd);

    Task<PagedResult<TenantNavigationAssignmentViewModel>> ListNavigationForTenantAsync(
        ClaimsPrincipal user, int tenantId, ListQuery query);
    Task<bool> SetNavigationForTenantAsync(
        ClaimsPrincipal user, int tenantId, int navigationMenuId, bool assigned);
}
