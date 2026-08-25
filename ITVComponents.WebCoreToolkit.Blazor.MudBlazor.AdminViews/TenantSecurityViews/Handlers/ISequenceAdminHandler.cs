using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface ISequenceAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<SequenceViewModel>> ListAsync(ClaimsPrincipal user, int? tenantId, ListQuery query);
    Task<SequenceViewModel?> CreateAsync(ClaimsPrincipal user, int? tenantId, SequenceViewModel input);
    Task<SequenceViewModel?> UpdateAsync(ClaimsPrincipal user, SequenceViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int sequenceId);
}
