using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface ITenantAdminHandler
{
    AdminContext GetContext(ClaimsPrincipal user);

    bool UseHierarchy { get; }
    
    bool HasPermission(params string[] permissions);

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

    Task<TenantTemplateViewModel?> ExtractTemplateAsync(
        ClaimsPrincipal user, int tenantId, string name, string? description);

    /// <summary>
    /// Re-applies the template attached to the tenant's own <c>TenantType</c> to that tenant, using
    /// <paramref name="defaultMode"/> as the default apply mode (per-kind template modes still win). Returns false when
    /// the caller lacks permission, the tenant is not visible/found, or has no type/template.
    /// </summary>
    Task<bool> ReapplyTenantTemplateAsync(ClaimsPrincipal user, int tenantId, TemplateApplyMode defaultMode);
}
