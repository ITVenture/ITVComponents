using System.Security.Claims;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers;

public interface IFeatureAdminHandler
{
    bool HasPermission(ClaimsPrincipal user, params string[] permissions);

    Task<PagedResult<FeatureViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query);
    Task<FeatureViewModel?> CreateAsync(ClaimsPrincipal user, FeatureViewModel input);
    Task<FeatureViewModel?> UpdateAsync(ClaimsPrincipal user, FeatureViewModel input);
    Task<bool> DeleteAsync(ClaimsPrincipal user, int featureId);

    Task<PagedResult<TemplateModuleViewModel>> ListTemplateModulesAsync(ClaimsPrincipal user, int featureId, ListQuery query);
    Task<TemplateModuleViewModel?> CreateTemplateModuleAsync(ClaimsPrincipal user, int featureId, TemplateModuleViewModel input);
    Task<TemplateModuleViewModel?> UpdateTemplateModuleAsync(ClaimsPrincipal user, TemplateModuleViewModel input);
    Task<bool> DeleteTemplateModuleAsync(ClaimsPrincipal user, int templateModuleId);

    Task<PagedResult<TemplateModuleConfiguratorViewModel>> ListConfiguratorsAsync(ClaimsPrincipal user, int templateModuleId, ListQuery query);
    Task<TemplateModuleConfiguratorViewModel?> CreateConfiguratorAsync(ClaimsPrincipal user, int templateModuleId, TemplateModuleConfiguratorViewModel input);
    Task<TemplateModuleConfiguratorViewModel?> UpdateConfiguratorAsync(ClaimsPrincipal user, TemplateModuleConfiguratorViewModel input);
    Task<bool> DeleteConfiguratorAsync(ClaimsPrincipal user, int templateModuleConfiguratorId);

    Task<PagedResult<TemplateModuleScriptViewModel>> ListScriptsAsync(ClaimsPrincipal user, int templateModuleId, ListQuery query);
    Task<TemplateModuleScriptViewModel?> CreateScriptAsync(ClaimsPrincipal user, int templateModuleId, TemplateModuleScriptViewModel input);
    Task<TemplateModuleScriptViewModel?> UpdateScriptAsync(ClaimsPrincipal user, TemplateModuleScriptViewModel input);
    Task<bool> DeleteScriptAsync(ClaimsPrincipal user, int templateModuleScriptId);

    Task<PagedResult<TemplateModuleConfiguratorParameterViewModel>> ListConfiguratorParametersAsync(ClaimsPrincipal user, int templateModuleConfiguratorId, ListQuery query);
    Task<TemplateModuleConfiguratorParameterViewModel?> CreateConfiguratorParameterAsync(ClaimsPrincipal user, int templateModuleConfiguratorId, TemplateModuleConfiguratorParameterViewModel input);
    Task<TemplateModuleConfiguratorParameterViewModel?> UpdateConfiguratorParameterAsync(ClaimsPrincipal user, TemplateModuleConfiguratorParameterViewModel input);
    Task<bool> DeleteConfiguratorParameterAsync(ClaimsPrincipal user, int templateModuleCfgParameterId);
}
