using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;

public interface IModuleVideoAdminHandler
{
    bool HasPermission(params string[] permissions);

    Task<PagedResult<VideoTutorialViewModel>> ListTutorialsAsync(ClaimsPrincipal user, ListQuery query);
    Task<VideoTutorialViewModel?> CreateTutorialAsync(ClaimsPrincipal user, VideoTutorialViewModel input);
    Task<VideoTutorialViewModel?> UpdateTutorialAsync(ClaimsPrincipal user, VideoTutorialViewModel input);
    Task<bool> DeleteTutorialAsync(ClaimsPrincipal user, int videoTutorialId);

    Task<PagedResult<TutorialStreamViewModel>> ListStreamsAsync(ClaimsPrincipal user, int videoTutorialId, ListQuery query);
    Task<TutorialStreamViewModel?> UpdateStreamAsync(ClaimsPrincipal user, TutorialStreamViewModel input);
    Task<bool> DeleteStreamAsync(ClaimsPrincipal user, int tutorialStreamId);
}
