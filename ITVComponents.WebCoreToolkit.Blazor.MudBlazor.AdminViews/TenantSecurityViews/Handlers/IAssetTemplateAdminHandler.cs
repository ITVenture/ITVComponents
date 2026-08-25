using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IAssetTemplateAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<AssetTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<AssetTemplateViewModel?> CreateAsync(ClaimsPrincipal user, AssetTemplateViewModel input);
    Task<AssetTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, AssetTemplateViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int assetTemplateId);

    Task<PagedResult<AssetTemplatePathViewModel>> ListPathsAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<AssetTemplatePathViewModel?> CreatePathAsync(ClaimsPrincipal user, int assetTemplateId, AssetTemplatePathViewModel input);
    Task<AssetTemplatePathViewModel?> UpdatePathAsync(ClaimsPrincipal user, AssetTemplatePathViewModel input);
    Task<bool> DeletePathAsync(ClaimsPrincipal user, int assetTemplatePathId);

    Task<PagedResult<AssetTemplatePermissionAssignmentViewModel>> ListPermissionsForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<bool> SetPermissionForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, int permissionId, bool assigned);

    Task<PagedResult<AssetTemplateFeatureAssignmentViewModel>> ListFeaturesForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, ListQuery query);
    Task<bool> SetFeatureForTemplateAsync(ClaimsPrincipal user, int assetTemplateId, int featureId, bool assigned);
}
