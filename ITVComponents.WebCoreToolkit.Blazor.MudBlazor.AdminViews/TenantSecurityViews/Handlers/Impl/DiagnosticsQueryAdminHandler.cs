using System.Security.Claims;
using ITVComponents.WebCoreToolkit.Blazor.SharedComponents.ForeignKeys;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class DiagnosticsQueryAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IDiagnosticsQueryAdminHandler
    where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
        TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
        TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
        TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet,
        TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin,
        TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation,
        TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
    where TTenant : Tenant
    where TUser : class
    where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>
    where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new()
    where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new()
    where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TQuery, TQueryParameter, TTenantQuery>, new()
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
    where TTenantSetting : TenantSetting<TTenant>
    where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
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

    public DiagnosticsQueryAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.services = services;
    }

    private TContext CreateDb()
    {
        var db = dbFactory.CreateDbContext();
        db.ShowAllTenants = true;
        db.HideGlobals = false;
        return db;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<DiagnosticsQueryViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "DiagnosticsQueries.View", "DiagnosticsQueries.Write"))
            return new PagedResult<DiagnosticsQueryViewModel>();

        using var db = CreateDb();
        var q = db.DiagnosticsQueries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(d => d.DiagnosticsQueryName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(d => d.DiagnosticsQueryName) : q.OrderBy(d => d.DiagnosticsQueryName);

        var rawItems = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(d => new
            {
                d.DiagnosticsQueryId,
                d.DiagnosticsQueryName,
                d.DbContext,
                d.AutoReturn,
                d.QueryText,
                d.PermissionId,
                Tenants = d.Tenants.Select(t => t.TenantId).ToArray()
            }).ToListAsync();

        var items = rawItems.Select(r => new DiagnosticsQueryViewModel
        {
            DiagnosticsQueryId = r.DiagnosticsQueryId,
            DiagnosticsQueryName = r.DiagnosticsQueryName,
            DbContext = r.DbContext,
            AutoReturn = r.AutoReturn,
            QueryText = r.QueryText,
            PermissionId = r.PermissionId,
            Tenants = r.Tenants
        }).ToList();
        return new PagedResult<DiagnosticsQueryViewModel> { Items = items, TotalCount = total };
    }

    public async Task<DiagnosticsQueryViewModel?> CreateAsync(ClaimsPrincipal user, DiagnosticsQueryViewModel input)
    {
        if (!HasPermission(user, "DiagnosticsQueries.Write")) return null;
        using var db = CreateDb();
        var entity = new TQuery
        {
            DiagnosticsQueryName = input.DiagnosticsQueryName,
            DbContext = input.DbContext,
            AutoReturn = input.AutoReturn,
            QueryText = input.QueryText ?? string.Empty,
            PermissionId = input.PermissionId
        };
        db.DiagnosticsQueries.Add(entity);
        await db.SaveChangesAsync();

        ApplyTenants(db, entity, input.Tenants);
        await db.SaveChangesAsync();

        input.DiagnosticsQueryId = entity.DiagnosticsQueryId;
        return input;
    }

    public async Task<DiagnosticsQueryViewModel?> UpdateAsync(ClaimsPrincipal user, DiagnosticsQueryViewModel input)
    {
        if (!HasPermission(user, "DiagnosticsQueries.Write")) return null;
        using var db = CreateDb();
        var entity = await db.DiagnosticsQueries
            .Include(d => d.Tenants)
            .FirstOrDefaultAsync(d => d.DiagnosticsQueryId == input.DiagnosticsQueryId);
        if (entity == null) return null;
        entity.DiagnosticsQueryName = input.DiagnosticsQueryName;
        entity.DbContext = input.DbContext;
        entity.AutoReturn = input.AutoReturn;
        entity.QueryText = input.QueryText ?? string.Empty;
        entity.PermissionId = input.PermissionId;

        ApplyTenants(db, entity, input.Tenants);
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int diagnosticsQueryId)
    {
        if (!HasPermission(user, "DiagnosticsQueries.Write")) return false;
        using var db = CreateDb();
        var entity = await db.DiagnosticsQueries
            .Include(d => d.Tenants)
            .Include(d => d.Parameters)
            .FirstOrDefaultAsync(d => d.DiagnosticsQueryId == diagnosticsQueryId);
        if (entity == null) return false;

        db.TenantDiagnosticsQueries.RemoveRange(entity.Tenants);
        db.DiagnosticsQueryParameters.RemoveRange(entity.Parameters);
        db.DiagnosticsQueries.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<DiagnosticsQueryParameterViewModel>> ListParametersAsync(ClaimsPrincipal user, int diagnosticsQueryId, ListQuery query)
    {
        if (!HasPermission(user, "DiagnosticsQueries.View", "DiagnosticsQueries.Write"))
            return new PagedResult<DiagnosticsQueryParameterViewModel>();

        using var db = CreateDb();
        var q = db.DiagnosticsQueryParameters.AsNoTracking().Where(p => p.DiagnosticsQueryId == diagnosticsQueryId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.ParameterName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(p => new DiagnosticsQueryParameterViewModel
            {
                DiagnosticsQueryParameterId = p.DiagnosticsQueryParameterId,
                DiagnosticsQueryId = p.DiagnosticsQueryId,
                ParameterName = p.ParameterName,
                ParameterType = p.ParameterType,
                Format = p.Format,
                Optional = p.Optional,
                DefaultValue = p.DefaultValue
            }).ToListAsync();
        return new PagedResult<DiagnosticsQueryParameterViewModel> { Items = items, TotalCount = total };
    }

    public async Task<DiagnosticsQueryParameterViewModel?> CreateParameterAsync(ClaimsPrincipal user, int diagnosticsQueryId, DiagnosticsQueryParameterViewModel input)
    {
        if (!HasPermission(user, "DiagnosticsQueries.Write")) return null;
        using var db = CreateDb();
        var entity = new TQueryParameter
        {
            DiagnosticsQueryId = diagnosticsQueryId,
            ParameterName = input.ParameterName,
            ParameterType = input.ParameterType,
            Format = input.Format ?? string.Empty,
            Optional = input.Optional,
            DefaultValue = input.DefaultValue ?? string.Empty
        };
        db.DiagnosticsQueryParameters.Add(entity);
        await db.SaveChangesAsync();
        input.DiagnosticsQueryParameterId = entity.DiagnosticsQueryParameterId;
        input.DiagnosticsQueryId = diagnosticsQueryId;
        return input;
    }

    public async Task<DiagnosticsQueryParameterViewModel?> UpdateParameterAsync(ClaimsPrincipal user, DiagnosticsQueryParameterViewModel input)
    {
        if (!HasPermission(user, "DiagnosticsQueries.Write")) return null;
        using var db = CreateDb();
        var entity = await db.DiagnosticsQueryParameters.FirstOrDefaultAsync(p => p.DiagnosticsQueryParameterId == input.DiagnosticsQueryParameterId);
        if (entity == null) return null;
        entity.ParameterName = input.ParameterName;
        entity.ParameterType = input.ParameterType;
        entity.Format = input.Format ?? string.Empty;
        entity.Optional = input.Optional;
        entity.DefaultValue = input.DefaultValue ?? string.Empty;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteParameterAsync(ClaimsPrincipal user, int diagnosticsQueryParameterId)
    {
        if (!HasPermission(user, "DiagnosticsQueries.Write")) return false;
        using var db = CreateDb();
        var entity = await db.DiagnosticsQueryParameters.FirstOrDefaultAsync(p => p.DiagnosticsQueryParameterId == diagnosticsQueryParameterId);
        if (entity == null) return false;
        db.DiagnosticsQueryParameters.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<TenantChoice>> ListAllTenantsAsync(ClaimsPrincipal user)
    {
        if (!HasPermission(user, "DiagnosticsQueries.View", "DiagnosticsQueries.Write"))
            return Array.Empty<TenantChoice>();

        using var db = CreateDb();
        return await db.Tenants.AsNoTracking()
            .OrderBy(t => t.DisplayName)
            .Select(t => new TenantChoice
            {
                TenantId = t.TenantId,
                DisplayName = t.DisplayName
            }).ToListAsync();
    }

    private void ApplyTenants(TContext db, TQuery entity, int[] tenantIds)
    {
        var current = entity.Tenants.ToList();
        var toRemove = current.Where(t => !tenantIds.Contains(t.TenantId)).ToList();
        var existingIds = current.Select(t => t.TenantId).ToHashSet();
        var toAdd = tenantIds.Where(id => !existingIds.Contains(id))
            .Select(id => new TTenantQuery { TenantId = id, DiagnosticsQuery = entity })
            .ToList();

        db.TenantDiagnosticsQueries.RemoveRange(toRemove);
        db.TenantDiagnosticsQueries.AddRange(toAdd);
    }
}
