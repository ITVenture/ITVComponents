using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class NavigationAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : INavigationAdminHandler
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
    where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation>, new()
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

    public NavigationAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
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

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<NavigationMenuViewModel>> ListAsync(ClaimsPrincipal user, int? parentId, ListQuery query)
    {
        if (!HasPermission("Navigation.View", "Navigation.Write"))
            return new PagedResult<NavigationMenuViewModel>();

        using var db = CreateDb();
        var q = db.Navigation.AsNoTracking().Where(n => n.ParentId == parentId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.DisplayName.Contains(s) || (n.Url != null && n.Url.Contains(s)));
        }
        var total = await q.CountAsync();
        var sorted = q.OrderBy(n => n.SortOrder ?? int.MaxValue).ThenBy(n => n.DisplayName);

        var rawItems = await sorted.Skip(query.Page * query.PageSize).Take(query.PageSize)
            .Select(n => new
            {
                n.NavigationMenuId,
                n.DisplayName,
                n.Url,
                n.ParentId,
                n.SortOrder,
                n.PermissionId,
                n.FeatureId,
                n.SpanClass,
                n.IsPublic,
                n.Metadata,
                Tenants = n.Tenants.Select(t => t.TenantId).ToArray(),
                ChildCount = n.Children.Count()
            }).ToListAsync();

        var items = rawItems.Select(r => new NavigationMenuViewModel
        {
            NavigationMenuId = r.NavigationMenuId,
            DisplayName = r.DisplayName,
            Url = r.Url,
            ParentId = r.ParentId,
            SortOrder = r.SortOrder,
            PermissionId = r.PermissionId,
            FeatureId = r.FeatureId,
            SpanClass = r.SpanClass,
            IsPublic = r.IsPublic,
            Metadata = r.Metadata,
            Tenants = r.Tenants,
            ChildCount = r.ChildCount
        }).ToList();

        return new PagedResult<NavigationMenuViewModel> { Items = items, TotalCount = total };
    }

    public async Task<NavigationMenuViewModel?> CreateAsync(ClaimsPrincipal user, NavigationMenuViewModel input)
    {
        if (!HasPermission("Navigation.Write")) return null;
        using var db = CreateDb();
        var maxOrder = await db.Navigation.Where(n => n.ParentId == input.ParentId)
            .MaxAsync(n => (int?)n.SortOrder) ?? 0;

        var entity = new TNavigationMenu
        {
            DisplayName = input.DisplayName,
            Url = input.Url ?? string.Empty,
            ParentId = input.ParentId,
            SortOrder = input.SortOrder ?? (maxOrder + 1),
            PermissionId = input.PermissionId,
            FeatureId = input.FeatureId,
            SpanClass = input.SpanClass ?? string.Empty,
            IsPublic = input.IsPublic,
            Metadata = string.IsNullOrWhiteSpace(input.Metadata) ? null : input.Metadata,
            RefTag = Guid.NewGuid().ToString("D")
        };
        db.Navigation.Add(entity);
        await db.SaveChangesAsync();

        ApplyTenants(db, entity, input.Tenants);
        await db.SaveChangesAsync();

        input.NavigationMenuId = entity.NavigationMenuId;
        input.SortOrder = entity.SortOrder;
        return input;
    }

    public async Task<NavigationMenuViewModel?> UpdateAsync(ClaimsPrincipal user, NavigationMenuViewModel input)
    {
        if (!HasPermission("Navigation.Write")) return null;
        using var db = CreateDb();
        var entity = await db.Navigation
            .Include(n => n.Tenants)
            .FirstOrDefaultAsync(n => n.NavigationMenuId == input.NavigationMenuId);
        if (entity == null) return null;

        entity.DisplayName = input.DisplayName;
        entity.Url = input.Url ?? string.Empty;
        entity.ParentId = input.ParentId;
        entity.SortOrder = input.SortOrder;
        entity.PermissionId = input.PermissionId;
        entity.FeatureId = input.FeatureId;
        entity.SpanClass = input.SpanClass ?? string.Empty;
        entity.IsPublic = input.IsPublic;
        entity.Metadata = string.IsNullOrWhiteSpace(input.Metadata) ? null : input.Metadata;

        ApplyTenants(db, entity, input.Tenants);
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int navigationMenuId)
    {
        if (!HasPermission("Navigation.Write")) return false;
        using var db = CreateDb();
        var entity = await db.Navigation
            .Include(n => n.Tenants)
            .FirstOrDefaultAsync(n => n.NavigationMenuId == navigationMenuId);
        if (entity == null) return false;

        db.TenantNavigation.RemoveRange(entity.Tenants);
        db.Navigation.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MoveAsync(ClaimsPrincipal user, int draggedItemId, int? anchorItemId, NavigationMoveAnchor anchor)
    {
        if (!HasPermission("Navigation.Write")) return false;
        if (draggedItemId == anchorItemId) return false;

        using var db = CreateDb();
        var dragged = await db.Navigation.FirstOrDefaultAsync(n => n.NavigationMenuId == draggedItemId);
        if (dragged == null) return false;

        int? newParentId;
        TNavigationMenu? anchorItem = null;
        if (anchorItemId is null)
        {
            newParentId = null;
        }
        else
        {
            anchorItem = await db.Navigation.FirstOrDefaultAsync(n => n.NavigationMenuId == anchorItemId.Value);
            if (anchorItem == null) return false;
            newParentId = anchor == NavigationMoveAnchor.Into ? anchorItem.NavigationMenuId : anchorItem.ParentId;
        }

        if (newParentId.HasValue && await IsDescendantOrSelfAsync(db, newParentId.Value, draggedItemId)) return false;

        var siblings = await db.Navigation
            .Where(n => n.ParentId == newParentId && n.NavigationMenuId != draggedItemId)
            .OrderBy(n => n.SortOrder ?? int.MaxValue).ThenBy(n => n.NavigationMenuId)
            .ToListAsync();

        const int step = 1000;
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = (i + 1) * step;

        int targetIndex;
        if (anchorItem is null || anchor == NavigationMoveAnchor.Into)
        {
            targetIndex = siblings.Count;
        }
        else
        {
            var anchorIndex = siblings.FindIndex(n => n.NavigationMenuId == anchorItem.NavigationMenuId);
            if (anchorIndex < 0) anchorIndex = siblings.Count;
            targetIndex = anchor == NavigationMoveAnchor.Above ? anchorIndex : anchorIndex + 1;
        }

        var prevOrder = targetIndex == 0 ? 0 : (siblings[targetIndex - 1].SortOrder ?? 0);
        var nextOrder = targetIndex >= siblings.Count ? prevOrder + 2 * step : (siblings[targetIndex].SortOrder ?? prevOrder + 2 * step);
        dragged.ParentId = newParentId;
        dragged.SortOrder = (prevOrder + nextOrder) / 2;

        await db.SaveChangesAsync();
        return true;
    }

    private async Task<bool> IsDescendantOrSelfAsync(TContext db, int candidateId, int draggedId)
    {
        if (candidateId == draggedId) return true;
        var current = candidateId;
        var guard = 0;
        while (true)
        {
            if (++guard > 256) return true;
            var parentId = await db.Navigation
                .Where(n => n.NavigationMenuId == current)
                .Select(n => n.ParentId)
                .FirstOrDefaultAsync();
            if (parentId is null) return false;
            if (parentId.Value == draggedId) return true;
            current = parentId.Value;
        }
    }

    public async Task<IReadOnlyList<NavigationParentChoice>> ListAllNavigationItemsAsync(ClaimsPrincipal user)
    {
        if (!HasPermission("Navigation.View", "Navigation.Write"))
            return Array.Empty<NavigationParentChoice>();

        using var db = CreateDb();
        return await db.Navigation.AsNoTracking()
            .OrderBy(n => n.DisplayName)
            .Select(n => new NavigationParentChoice
            {
                NavigationMenuId = n.NavigationMenuId,
                DisplayName = n.DisplayName
            }).ToListAsync();
    }

    public async Task<IReadOnlyList<TenantChoice>> ListAllTenantsAsync(ClaimsPrincipal user)
    {
        if (!HasPermission("Navigation.View", "Navigation.Write"))
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

    private void ApplyTenants(TContext db, TNavigationMenu entity, int[] tenantIds)
    {
        var current = entity.Tenants.ToList();
        var toRemove = current.Where(t => !tenantIds.Contains(t.TenantId)).ToList();
        var existingIds = current.Select(t => t.TenantId).ToHashSet();
        var toAdd = tenantIds.Where(id => !existingIds.Contains(id))
            .Select(id => new TTenantNavigation { TenantId = id, NavigationMenu = entity })
            .ToList();

        db.TenantNavigation.RemoveRange(toRemove);
        db.TenantNavigation.AddRange(toAdd);
    }
}
