using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IDiagnosticsQueryAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<DiagnosticsQueryViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<DiagnosticsQueryViewModel?> CreateAsync(ClaimsPrincipal user, DiagnosticsQueryViewModel input);
    Task<DiagnosticsQueryViewModel?> UpdateAsync(ClaimsPrincipal user, DiagnosticsQueryViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int diagnosticsQueryId);

    Task<PagedResult<DiagnosticsQueryParameterViewModel>> ListParametersAsync(ClaimsPrincipal user, int diagnosticsQueryId, ListQuery query);
    Task<DiagnosticsQueryParameterViewModel?> CreateParameterAsync(ClaimsPrincipal user, int diagnosticsQueryId, DiagnosticsQueryParameterViewModel input);
    Task<DiagnosticsQueryParameterViewModel?> UpdateParameterAsync(ClaimsPrincipal user, DiagnosticsQueryParameterViewModel input);
    Task<bool> DeleteParameterAsync(ClaimsPrincipal user, int diagnosticsQueryParameterId);

    Task<IReadOnlyList<TenantChoice>> ListAllTenantsAsync(ClaimsPrincipal user);
}
