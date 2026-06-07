using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IDbResourceAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<CultureViewModel>> ListCulturesAsync(ClaimsPrincipal user, ListQuery query);
    Task<CultureViewModel?> CreateCultureAsync(ClaimsPrincipal user, CultureViewModel input);
    Task<CultureViewModel?> UpdateCultureAsync(ClaimsPrincipal user, CultureViewModel input);
    Task<bool> DeleteCultureAsync(ClaimsPrincipal user, int cultureId);

    Task<PagedResult<LocalizationViewModel>> ListLocalizationsAsync(ClaimsPrincipal user, ListQuery query);
    Task<LocalizationViewModel?> CreateLocalizationAsync(ClaimsPrincipal user, LocalizationViewModel input);
    Task<LocalizationViewModel?> UpdateLocalizationAsync(ClaimsPrincipal user, LocalizationViewModel input);
    Task<bool> DeleteLocalizationAsync(ClaimsPrincipal user, int localizationId);

    Task<PagedResult<LocalizationCultureViewModel>> ListLocalizationCulturesAsync(ClaimsPrincipal user, int localizationId, ListQuery query);
    Task<LocalizationCultureViewModel?> CreateLocalizationCultureAsync(ClaimsPrincipal user, int localizationId, LocalizationCultureViewModel input);
    Task<bool> DeleteLocalizationCultureAsync(ClaimsPrincipal user, int localizationCultureId);

    Task<PagedResult<LocalizationStringViewModel>> ListStringsAsync(ClaimsPrincipal user, int localizationCultureId, ListQuery query);
    Task<LocalizationStringViewModel?> CreateStringAsync(ClaimsPrincipal user, int localizationCultureId, LocalizationStringViewModel input);
    Task<LocalizationStringViewModel?> UpdateStringAsync(ClaimsPrincipal user, LocalizationStringViewModel input);
    Task<bool> DeleteStringAsync(ClaimsPrincipal user, int localizationStringId);
}
