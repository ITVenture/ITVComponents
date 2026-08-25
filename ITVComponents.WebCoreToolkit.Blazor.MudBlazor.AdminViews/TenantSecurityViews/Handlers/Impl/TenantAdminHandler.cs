using System.Security.Claims;
using ITVComponents.Json;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class TenantAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : ITenantAdminHandler
    where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
        TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
        TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
        TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
        TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
        TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation,
        TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
    where TTenant : Tenant, new()
    where TUser : class
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>, new()
    where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>, new()
    where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>
    where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
    where TUserProperty : CustomUserProperty<TUserId, TUser>
    where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
    where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
    where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
    where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
    where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
    where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TWebPluginConstant : WebPluginConstant<TTenant>
    where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
    where TSequence : Sequence<TTenant>
    where TTenantSetting : TenantSetting<TTenant>, new()
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>, new()
    where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTrustConfig : BaseTenantContextSecurityTrustConfig<TTrustConfig>, new()
    where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly IServiceProvider services;
    private readonly ISecurityRepository securityRepository;
    private readonly IOptions<TenantOptions<TTenant>> tenantOptions;
    private readonly ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> templateHelper;

    public TenantAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services, ISecurityRepository securityRepository, IOptions<TenantOptions<TTenant>> tenantOptions, ITenantTemplateHelper<TTenant, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> templateHelper)
    {
        this.dbFactory = dbFactory;
        this.services = services;
        this.securityRepository = securityRepository;
        this.tenantOptions = tenantOptions;
        this.templateHelper = templateHelper;
    }
    
    public bool UseHierarchy => tenantOptions.Value.UseHierarchy;

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public AdminContext GetContext(ClaimsPrincipal user)
    {
        using var db = dbFactory.CreateDbContext();
        var sysAdmin = IsSysAdmin();
        ApplyContextScope(db, sysAdmin);
        return new AdminContext
        {
            IsSysAdmin = sysAdmin,
            CurrentTenantId = db.CurrentTenantId
        };
    }

    public async Task<PagedResult<TenantViewModel>> ListTenantsAsync(ClaimsPrincipal user, ListQuery query)
    {
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());
        var q = db.Tenants.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.TenantName.Contains(s) || (t.DisplayName != null && t.DisplayName.Contains(s)));
        }
        var total = await q.CountAsync();
        q = (query.SortColumn?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("displayname", true) => q.OrderByDescending(t => t.DisplayName),
            ("displayname", false) => q.OrderBy(t => t.DisplayName),
            (_, true) => q.OrderByDescending(t => t.TenantName),
            _ => q.OrderBy(t => t.TenantName)
        };
        var tenantSelect = tenantOptions.Value.SelectTenant;
        if (tenantSelect == null)
        {
            tenantSelect = t => new TenantViewModel
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                DisplayName = t.DisplayName,
                TimeZone = t.TimeZone,
                TenantTypeId = t.TenantTypeId
            };
        }
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(tenantSelect).ToListAsync();

        // Flag which tenants can have their TenantType's template re-applied (type assigned + type carries a template).
        // Done as a separate lookup (not in the projection) so it also holds when a custom SelectTenant is configured.
        var typeIds = items.Where(i => i.TenantTypeId != null).Select(i => i.TenantTypeId!.Value).Distinct().ToArray();
        if (typeIds.Length > 0)
        {
            var typesWithTemplate = (await db.TenantTypes.AsNoTracking()
                .Where(tt => typeIds.Contains(tt.TenantTypeId) && tt.TenantTemplateId != null)
                .Select(tt => tt.TenantTypeId).ToListAsync()).ToHashSet();
            foreach (var it in items)
            {
                it.CanReapplyTemplate = it.TenantTypeId != null && typesWithTemplate.Contains(it.TenantTypeId.Value);
            }
        }

        return new PagedResult<TenantViewModel> { Items = items, TotalCount = total };
    }

    public async Task<TenantViewModel?> CreateTenantAsync(ClaimsPrincipal user, TenantViewModel input)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.Write" })) return null;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());
        var entity = new TTenant();
        var tenantAssign = tenantOptions.Value.UpdateTenant;
        if (tenantAssign == null)
        {
            entity.TenantName = input.TenantName;
            entity.DisplayName = input.DisplayName;
            entity.TimeZone = input.TimeZone;
            entity.TenantTypeId = input.TenantTypeId;
        }
        else
        {
            tenantAssign(entity, input);
        }

        db.Tenants.Add(entity);
        await db.SaveChangesAsync();
        input.TenantId = entity.TenantId;
        return input;
    }

    public async Task<TenantViewModel?> UpdateTenantAsync(ClaimsPrincipal user, TenantViewModel input)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.Write" })) return null;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());
        var entity = await db.Tenants.FirstOrDefaultAsync(n => n.TenantId == input.TenantId);
        if (entity == null) return null;
        var tenantAssign = tenantOptions.Value.UpdateTenant;
        if (tenantAssign == null)
        {
            tenantAssign = (e, i) =>
            {
                e.TenantName = i.TenantName;
                e.DisplayName = i.DisplayName;
                e.TimeZone = i.TimeZone;
                e.TenantTypeId = i.TenantTypeId;
            };
        }

        tenantAssign(entity, input);

        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteTenantAsync(ClaimsPrincipal user, int tenantId)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.Write" })) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());
        var entity = await db.Tenants.FirstOrDefaultAsync(n => n.TenantId == tenantId);
        if (entity == null) return false;
        db.Tenants.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<TenantAssignmentViewModel>> ListTenantAssignmentsForUserAsync(
        ClaimsPrincipal user, string userId, ListQuery query)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.AssignUser", "Tenants.View" }))
            return new PagedResult<TenantAssignmentViewModel>();

        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var assigned = await db.TenantUsers
            .Where(r => r.UserId.ToString() == userId)
            .Select(r => r.TenantId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assigned);

        var total = await db.Tenants.CountAsync();
        var items = await db.Tenants.AsNoTracking()
            .OrderBy(t => t.TenantName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(t => new TenantAssignmentViewModel
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                DisplayName = t.DisplayName,
                Assigned = false
            }).ToListAsync();
        foreach (var item in items)
        {
            item.Assigned = assignedSet.Contains(item.TenantId);
        }
        return new PagedResult<TenantAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetUserTenantAssignmentAsync(
        ClaimsPrincipal user, string userId, int tenantId, bool assigned)
    {
        if (!services.VerifyUserPermissions(new[] { "Tenants.AssignUser" })) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var existing = await db.TenantUsers
            .FirstOrDefaultAsync(n => n.UserId.ToString() == userId && n.TenantId == tenantId);

        if (assigned && existing == null)
        {
            var tu = new TTenantUser { TenantId = tenantId, UserId = ParseUserId(userId) };
            db.TenantUsers.Add(tu);
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.TenantUsers.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    public async Task<PagedResult<TenantSettingViewModel>> ListSettingsAsync(ClaimsPrincipal user, int tenantId, ListQuery query)
    {
        if (!HasPermission("Tenants.View", "Tenants.WriteSettings"))
            return new PagedResult<TenantSettingViewModel>();
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var q = db.TenantSettings.AsNoTracking().Where(s => s.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.SettingsKey.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(s => s.SettingsKey)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(s => new TenantSettingViewModel
            {
                TenantSettingId = s.TenantSettingId,
                TenantId = s.TenantId,
                SettingsKey = s.SettingsKey,
                SettingsValue = s.SettingsValue,
                JsonSetting = s.JsonSetting
            })
            .ToListAsync();
        return new PagedResult<TenantSettingViewModel> { Items = items, TotalCount = total };
    }

    public async Task<TenantSettingViewModel?> CreateSettingAsync(ClaimsPrincipal user, int tenantId, TenantSettingViewModel input)
    {
        if (!HasPermission("Tenants.WriteSettings")) return null;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var value = await MaybeEncryptJsonAsync(db, tenantId, input.JsonSetting, input.SettingsValue);

        var entity = new TTenantSetting
        {
            TenantId = tenantId,
            SettingsKey = input.SettingsKey,
            SettingsValue = value,
            JsonSetting = input.JsonSetting
        };
        db.TenantSettings.Add(entity);
        await db.SaveChangesAsync();
        input.TenantSettingId = entity.TenantSettingId;
        input.TenantId = tenantId;
        input.SettingsValue = value;
        return input;
    }

    public async Task<TenantSettingViewModel?> UpdateSettingAsync(ClaimsPrincipal user, TenantSettingViewModel input)
    {
        if (!HasPermission("Tenants.WriteSettings")) return null;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var entity = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantSettingId == input.TenantSettingId);
        if (entity == null) return null;
        entity.SettingsKey = input.SettingsKey;
        entity.SettingsValue = await MaybeEncryptJsonAsync(db, entity.TenantId, input.JsonSetting, input.SettingsValue);
        entity.JsonSetting = input.JsonSetting;
        await db.SaveChangesAsync();
        input.SettingsValue = entity.SettingsValue;
        return input;
    }

    private async Task<string> MaybeEncryptJsonAsync(TContext db, int tenantId, bool isJsonSetting, string raw)
    {
        if (!isJsonSetting || string.IsNullOrEmpty(raw)) return raw;
        var tenantPassword = await db.Tenants.AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.TenantPassword)
            .FirstOrDefaultAsync();
        // EncryptJsonValues uses the default encryptor when password is null/empty,
        // matching Telerik TenantControllerStruct.CreateSetting / UpdateSetting behaviour.
        return raw.EncryptJsonValues(tenantPassword);
    }

    public async Task<bool> DeleteSettingAsync(ClaimsPrincipal user, int tenantSettingId)
    {
        if (!HasPermission("Tenants.WriteSettings")) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var entity = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantSettingId == tenantSettingId);
        if (entity == null) return false;
        db.TenantSettings.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<TenantFeatureActivationAssignmentViewModel>> ListFeatureActivationsForTenantAsync(
        ClaimsPrincipal user, int tenantId, ListQuery query)
    {
        if (!HasPermission("Sysadmin"))
            return new PagedResult<TenantFeatureActivationAssignmentViewModel>();
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var q = from f in db.Features.AsNoTracking()
                join a in db.TenantFeatureActivations.Where(x => x.TenantId == tenantId)
                    on f.FeatureId equals a.FeatureId into aj
                from act in aj.DefaultIfEmpty()
                select new TenantFeatureActivationAssignmentViewModel
                {
                    TenantId = tenantId,
                    FeatureId = f.FeatureId,
                    FeatureName = f.FeatureName,
                    Assigned = act != null,
                    TenantFeatureActivationId = act != null ? (int?)act.TenantFeatureActivationId : null,
                    ActivationStart = act != null ? act.ActivationStart : null,
                    ActivationEnd = act != null ? act.ActivationEnd : null
                };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.FeatureName.Contains(s));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(x => x.FeatureName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<TenantFeatureActivationAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetFeatureActivationForTenantAsync(
        ClaimsPrincipal user, int tenantId, int featureId, bool assigned, DateTime? activationStart, DateTime? activationEnd)
    {
        if (!HasPermission("Sysadmin")) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var existing = await db.TenantFeatureActivations
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.FeatureId == featureId);

        if (assigned)
        {
            if (existing == null)
            {
                db.TenantFeatureActivations.Add(new TTenantFeatureActivation
                {
                    TenantId = tenantId,
                    FeatureId = featureId,
                    ActivationStart = activationStart,
                    ActivationEnd = activationEnd
                });
            }
            else
            {
                existing.ActivationStart = activationStart;
                existing.ActivationEnd = activationEnd;
            }
            await db.SaveChangesAsync();
            return true;
        }

        if (existing != null)
        {
            db.TenantFeatureActivations.Remove(existing);
            await db.SaveChangesAsync();
        }
        return true;
    }

    public async Task<PagedResult<TenantNavigationAssignmentViewModel>> ListNavigationForTenantAsync(
        ClaimsPrincipal user, int tenantId, ListQuery query)
    {
        if (!HasPermission("Tenants.AssignNav", "Sysadmin"))
            return new PagedResult<TenantNavigationAssignmentViewModel>();
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var q = from n in db.Navigation.AsNoTracking()
                join tn in db.TenantNavigation.Where(x => x.TenantId == tenantId)
                    on n.NavigationMenuId equals tn.NavigationMenuId into tnj
                from t in tnj.DefaultIfEmpty()
                select new TenantNavigationAssignmentViewModel
                {
                    TenantId = tenantId,
                    NavigationMenuId = n.NavigationMenuId,
                    DisplayName = n.DisplayName,
                    Url = n.Url,
                    Assigned = t != null
                };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.DisplayName.Contains(s) || (x.Url != null && x.Url.Contains(s)));
        }
        var total = await q.CountAsync();
        var items = await q.OrderBy(x => x.DisplayName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize).ToListAsync();
        return new PagedResult<TenantNavigationAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetNavigationForTenantAsync(
        ClaimsPrincipal user, int tenantId, int navigationMenuId, bool assigned)
    {
        if (!HasPermission("Tenants.AssignNav", "Sysadmin")) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var existing = await db.TenantNavigation
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.NavigationMenuId == navigationMenuId);

        if (assigned && existing == null)
        {
            db.TenantNavigation.Add(new TTenantNavigation
            {
                TenantId = tenantId,
                NavigationMenuId = navigationMenuId
            });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.TenantNavigation.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }

    public async Task<bool> ReapplyTenantTemplateAsync(ClaimsPrincipal user, int tenantId, TemplateApplyMode defaultMode)
    {
        if (!HasPermission("TenantTemplates.Write")) return false;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        // Scope-respecting lookup: a caller who cannot see the tenant gets null -> false.
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        if (tenant == null) return false;

        // Re-applies the tenant's own TenantType template (resolved + applied on the helper's own leased context).
        templateHelper.ApplyTenantTypeTemplate(tenant, defaultMode);
        return true;
    }

    public async Task<TenantTemplateViewModel?> ExtractTemplateAsync(ClaimsPrincipal user, int tenantId, string name, string? description)
    {
        if (!HasPermission("TenantTemplates.Write")) return null;
        using var db = dbFactory.CreateDbContext();
        ApplyContextScope(db, IsSysAdmin());

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        if (tenant == null) return null;

        var markup = templateHelper.ExtractTemplate(tenant);
        var json = JsonHelper.ToJson(markup, SerializationTypingMode.NativePolymorphism);
        var entity = new TenantTemplate
        {
            Name = name,
            Description = description ?? string.Empty,
            Markup = json
        };
        db.TenantTemplates.Add(entity);
        await db.SaveChangesAsync();
        return new TenantTemplateViewModel
        {
            TenantTemplateId = entity.TenantTemplateId,
            Name = entity.Name,
            Description = entity.Description,
            Markup = entity.Markup
        };
    }

    private static TUserId ParseUserId(string userId)
    {
        if (typeof(TUserId) == typeof(string))
            return (TUserId)(object)userId;
        if (typeof(TUserId) == typeof(int))
            return (TUserId)(object)int.Parse(userId);
        if (typeof(TUserId) == typeof(Guid))
            return (TUserId)(object)Guid.Parse(userId);
        throw new InvalidOperationException($"Unsupported user-id type {typeof(TUserId).Name}");
    }

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin });

    private void ApplyContextScope(TContext db, bool sysAdmin)
    {
        if (sysAdmin)
        {
            db.HideGlobals = false;
            db.ShowAllTenants = true;
        }
        else
        {
            db.HideGlobals = true;
            db.ShowAllTenants = false;
        }
    }
}
