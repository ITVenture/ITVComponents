using System.Security.Claims;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

/// <summary>
/// Die Verwaltung der Anwendungen eines Mandanten.
/// </summary>
/// <remarks>
/// <b>Mandantengefiltert</b> - der Kontext wird hier NICHT auf ShowAllTenants gestellt, anders als in der
/// globalen Template-Verwaltung. Eine Anwendung gehoert einem Mandanten, und ein Administrator sieht die
/// seines eigenen.
/// </remarks>
public class ClientAppAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp,
    TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IClientAppAdminHandler
    where TContext : DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
        TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery,
        TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty,
        TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset,
        TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp, TClientAppPermission, TClientAppAccess, TWebPlugin,
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
    where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new()
    where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new()
    where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>, new()
    where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>, new()
    where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>, new()
    where TClientAppAccess : ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
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
    private readonly ILogger logger;

    public ClientAppAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.services = services;
        logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ClientAppAdminHandler");
    }

    /// <summary>
    /// Der Kontext fuer die Anwendungen - MIT Mandantenfilter.
    /// </summary>
    private TContext CreateDb()
    {
        var db = dbFactory.CreateDbContext();
        db.HideGlobals = false;
        db.ShowAllTenants = false;
        return db;
    }

    /// <summary>
    /// Der Kontext fuer die Templates - OHNE Filter, weil Templates global sind.
    /// </summary>
    private TContext CreateGlobalDb()
    {
        var db = dbFactory.CreateDbContext();
        db.HideGlobals = false;
        db.ShowAllTenants = true;
        return db;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<ClientAppViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("Apps.View", "Apps.Write"))
        {
            return new PagedResult<ClientAppViewModel>();
        }

        using var db = CreateDb();
        var q = db.ClientApps.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.ClientName.Contains(s) || t.ClientKey.Contains(s));
        }

        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(t => t.ClientName) : q.OrderBy(t => t.ClientName);
        var items = await sorted.Page(t => t.ClientAppId, query)
            .Select(t => new ClientAppViewModel
            {
                ClientAppId = t.ClientAppId,
                ClientName = t.ClientName,
                ClientAppTemplateId = t.ClientAppTemplateId,
                ClientKey = t.ClientKey,
                Enabled = t.Enabled,
                CreatedUtc = t.CreatedUtc,
                ActiveAccessCount = t.Accesses.Count(a => a.RevokedUtc == null)
            }).ToListAsync();

        // Der Template-Name kommt aus dem ungefilterten Kontext - Templates sind global, die App-Abfrage
        // laeuft mandantengefiltert.
        if (items.Count != 0)
        {
            using var gdb = CreateGlobalDb();
            var ids = items.Select(n => n.ClientAppTemplateId).Distinct().ToArray();
            var names = await gdb.ClientAppTemplates.Where(n => ids.Contains(n.ClientAppTemplateId))
                .ToDictionaryAsync(n => n.ClientAppTemplateId, n => n.Name);
            foreach (var item in items)
            {
                item.TemplateName = names.TryGetValue(item.ClientAppTemplateId, out var nm) ? nm : "";
            }
        }

        return new PagedResult<ClientAppViewModel> { Items = items, TotalCount = total };
    }

    public async Task<IReadOnlyList<ClientAppTemplateViewModel>> ListTemplatesAsync(ClaimsPrincipal user)
    {
        if (!HasPermission("Apps.View", "Apps.Write"))
        {
            return Array.Empty<ClientAppTemplateViewModel>();
        }

        using var db = CreateGlobalDb();
        return await db.ClientAppTemplates.AsNoTracking().OrderBy(n => n.Name)
            .Select(n => new ClientAppTemplateViewModel { ClientAppTemplateId = n.ClientAppTemplateId, Name = n.Name })
            .ToListAsync();
    }

    public async Task<ClientAppViewModel?> CreateAsync(ClaimsPrincipal user, ClientAppViewModel input)
    {
        if (!HasPermission("Apps.Write"))
        {
            logger.LogWarning("Creating a client app was refused: Apps.Write is missing.");
            return null;
        }

        using var db = CreateDb();
        var tenantId = db.CurrentTenantId;
        if (tenantId == null)
        {
            // Ohne Mandantenkontext gaebe es keinen Eigentuemer. Das ist kein Randfall, den man
            // stillschweigend auf 0 abbilden darf.
            logger.LogError("Creating a client app failed: there is no current tenant scope.");
            return null;
        }

        using (var gdb = CreateGlobalDb())
        {
            if (!await gdb.ClientAppTemplates.AnyAsync(n => n.ClientAppTemplateId == input.ClientAppTemplateId))
            {
                logger.LogWarning("Creating a client app failed: template {TemplateId} does not exist.",
                    input.ClientAppTemplateId);
                return null;
            }
        }

        var entity = new TClientApp
        {
            TenantId = tenantId.Value,
            ClientAppTemplateId = input.ClientAppTemplateId,
            ClientName = input.ClientName,
            // Systemweit eindeutig und deshalb NICHT vom Aufrufer: eine Kennung, die jemand waehlen darf,
            // ist eine Kennung, die kollidiert.
            ClientKey = SecretHasher.CreateSecret(),
            ClientSecret = string.Empty,
            Enabled = input.Enabled,
            CreatedUtc = DateTime.UtcNow
        };
        db.ClientApps.Add(entity);
        await db.SaveChangesAsync();

        input.ClientAppId = entity.ClientAppId;
        input.ClientKey = entity.ClientKey;
        input.CreatedUtc = entity.CreatedUtc;
        logger.LogInformation("Client app {Name} created for tenant {TenantId}.", entity.ClientName, tenantId);
        return input;
    }

    public async Task<ClientAppViewModel?> UpdateAsync(ClaimsPrincipal user, ClientAppViewModel input)
    {
        if (!HasPermission("Apps.Write"))
        {
            logger.LogWarning("Updating client app {AppId} was refused: Apps.Write is missing.", input.ClientAppId);
            return null;
        }

        using var db = CreateDb();
        var entity = await db.ClientApps.FirstOrDefaultAsync(n => n.ClientAppId == input.ClientAppId);
        if (entity == null)
        {
            logger.LogWarning("Client app {AppId} does not exist in this tenant; nothing updated.", input.ClientAppId);
            return null;
        }

        // Template und Kennung bleiben: ein Template-Wechsel machte die zugeordneten Buendel ungueltig,
        // eine neue Kennung sperrte jedes gekoppelte Geraet aus.
        entity.ClientName = input.ClientName;
        entity.Enabled = input.Enabled;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int clientAppId)
    {
        if (!HasPermission("Apps.Write"))
        {
            logger.LogWarning("Deleting client app {AppId} was refused: Apps.Write is missing.", clientAppId);
            return false;
        }

        using var db = CreateDb();
        var entity = await db.ClientApps.FirstOrDefaultAsync(n => n.ClientAppId == clientAppId);
        if (entity == null)
        {
            logger.LogWarning("Client app {AppId} does not exist in this tenant; nothing deleted.", clientAppId);
            return false;
        }

        if (await db.ClientAppAccesses.AnyAsync(n => n.ClientAppId == clientAppId && n.RevokedUtc == null))
        {
            // Sonst verschwaende ein Klick still die Zugaenge mehrerer Geraete.
            logger.LogWarning(
                "Client app {AppId} still has active accesses and was not deleted. Revoke them first.", clientAppId);
            return false;
        }

        db.ClientApps.Remove(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Client app {AppId} deleted.", clientAppId);
        return true;
    }

    public async Task<PagedResult<ClientAppPermissionSetViewModel>> ListPermissionSetsAsync(ClaimsPrincipal user,
        int clientAppId, ListQuery query)
    {
        if (!HasPermission("Apps.View", "Apps.Write"))
        {
            return new PagedResult<ClientAppPermissionSetViewModel>();
        }

        using var db = CreateDb();
        var app = await db.ClientApps.AsNoTracking()
            .Where(n => n.ClientAppId == clientAppId)
            .Select(n => new { n.ClientAppTemplateId })
            .FirstOrDefaultAsync();
        if (app == null)
        {
            return new PagedResult<ClientAppPermissionSetViewModel>();
        }

        var assigned = new HashSet<int>(await db.ClientAppPermissions
            .Where(n => n.ClientAppId == clientAppId)
            .Select(n => n.AppPermissionSetId).ToListAsync());

        // Nur die Buendel DIESES Templates - das ist die Obergrenze, und sie ist hier eine simple
        // Bedingung statt einer Rechenregel.
        using var gdb = CreateGlobalDb();
        var q = gdb.AppPermissionSets.AsNoTracking()
            .Where(n => n.ClientAppTemplateId == app.ClientAppTemplateId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.Name.Contains(s));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.Name).Page(n => n.AppPermissionSetId, query)
            .Select(n => new ClientAppPermissionSetViewModel
            {
                AppPermissionSetId = n.AppPermissionSetId,
                ClientAppId = clientAppId,
                PermissionSetName = n.Name,
                Permissions = string.Join(", ", n.Permissions.Select(p => p.Permission.PermissionName))
            }).ToListAsync();
        foreach (var item in items)
        {
            item.Assigned = assigned.Contains(item.AppPermissionSetId);
        }

        return new PagedResult<ClientAppPermissionSetViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetPermissionSetAsync(ClaimsPrincipal user, int clientAppId, int appPermissionSetId,
        bool assigned)
    {
        if (!HasPermission("Apps.Write"))
        {
            logger.LogWarning("Changing permission sets of client app {AppId} was refused: Apps.Write is missing.",
                clientAppId);
            return false;
        }

        using var db = CreateDb();
        var app = await db.ClientApps.AsNoTracking()
            .Where(n => n.ClientAppId == clientAppId)
            .Select(n => new { n.ClientAppTemplateId })
            .FirstOrDefaultAsync();
        if (app == null)
        {
            logger.LogWarning("Client app {AppId} does not exist in this tenant.", clientAppId);
            return false;
        }

        if (assigned)
        {
            // DIE OBERGRENZE, als Pruefung: ein Buendel eines fremden Templates waere ein Recht, das die
            // Plattform fuer diese Anwendung nie vorgesehen hat.
            using var gdb = CreateGlobalDb();
            var belongs = await gdb.AppPermissionSets.AnyAsync(n =>
                n.AppPermissionSetId == appPermissionSetId && n.ClientAppTemplateId == app.ClientAppTemplateId);
            if (!belongs)
            {
                logger.LogWarning(
                    "Permission set {SetId} does not belong to the template of client app {AppId} and was refused.",
                    appPermissionSetId, clientAppId);
                return false;
            }
        }

        var existing = await db.ClientAppPermissions.FirstOrDefaultAsync(n =>
            n.ClientAppId == clientAppId && n.AppPermissionSetId == appPermissionSetId);
        if (assigned && existing == null)
        {
            db.ClientAppPermissions.Add(new TClientAppPermission
            {
                ClientAppId = clientAppId,
                AppPermissionSetId = appPermissionSetId
            });
            await db.SaveChangesAsync();
        }
        else if (!assigned && existing != null)
        {
            db.ClientAppPermissions.Remove(existing);
            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<PagedResult<ClientAppAccessViewModel>> ListAccessesAsync(ClaimsPrincipal user, int clientAppId,
        ListQuery query)
    {
        if (!HasPermission("Apps.View", "Apps.Write"))
        {
            return new PagedResult<ClientAppAccessViewModel>();
        }

        using var db = CreateDb();
        var q = db.ClientAppAccesses.AsNoTracking().Where(n => n.ClientAppId == clientAppId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.DeviceLabel.Contains(s) || n.Label.Contains(s));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(n => n.CreatedUtc).Page(n => n.ClientAppAccessId, query)
            .Select(n => new ClientAppAccessViewModel
            {
                ClientAppAccessId = n.ClientAppAccessId,
                ClientAppId = n.ClientAppId,
                Label = n.Label,
                DeviceLabel = n.DeviceLabel,
                IsMachine = n.TenantUserId == null,
                CreatedUtc = n.CreatedUtc,
                ExpiresUtc = n.ExpiresUtc,
                RevokedUtc = n.RevokedUtc,
                LastUsedUtc = n.LastUsedUtc,
                HasSecret = n.SecretHash != null
            }).ToListAsync();

        return new PagedResult<ClientAppAccessViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> RevokeAccessAsync(ClaimsPrincipal user, int clientAppAccessId)
    {
        if (!HasPermission("Apps.Write"))
        {
            logger.LogWarning("Revoking access {AccessId} was refused: Apps.Write is missing.", clientAppAccessId);
            return false;
        }

        using var db = CreateDb();
        var entity = await db.ClientAppAccesses.FirstOrDefaultAsync(n => n.ClientAppAccessId == clientAppAccessId);
        if (entity == null)
        {
            logger.LogWarning("Access {AccessId} does not exist in this tenant; nothing revoked.", clientAppAccessId);
            return false;
        }

        if (entity.RevokedUtc != null)
        {
            logger.LogDebug("Access {AccessId} was already revoked.", clientAppAccessId);
            return true;
        }

        entity.RevokedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("Access {Label} was revoked.", entity.Label);
        return true;
    }
}
