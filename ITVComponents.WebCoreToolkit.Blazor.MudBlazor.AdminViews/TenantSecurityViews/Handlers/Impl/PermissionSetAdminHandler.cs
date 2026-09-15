using System.Security.Claims;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using ITVComponents.WebCoreToolkit.Blazor.Paging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class PermissionSetAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp,
    TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IPermissionSetAdminHandler
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
    where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
    where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
    where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
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

    public PermissionSetAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.services = services;
        // Ueber die Factory und mit festem Kategorienamen statt als Konstruktor-Parameter - wie im
        // AppTemplateAdminHandler: der Typ traegt vierzig Typparameter, sein Name waere als
        // Protokoll-Kategorie unbrauchbar.
        logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PermissionSetAdminHandler");
    }

    private TContext CreateDb()
    {
        var db = dbFactory.CreateDbContext();
        db.HideGlobals = false;
        db.ShowAllTenants = true;
        return db;
    }

    public bool HasPermission(params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    public async Task<PagedResult<PermissionSetViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("Apps.PermissionSets.View", "Apps.PermissionSets.Write"))
            return new PagedResult<PermissionSetViewModel>();

        using var db = CreateDb();
        var q = db.AppPermissionSets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(s));
        }
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(p => p.Name) : q.OrderBy(p => p.Name);
        var items = await sorted.Page(p => p.AppPermissionSetId, query)
            .Select(p => new PermissionSetViewModel
            {
                AppPermissionSetId = p.AppPermissionSetId,
                ClientAppTemplateId = p.ClientAppTemplateId,
                ClientAppTemplateName = p.ClientAppTemplate.Name,
                Name = p.Name
            }).ToListAsync();
        return new PagedResult<PermissionSetViewModel> { Items = items, TotalCount = total };
    }

    public async Task<IReadOnlyList<ClientAppTemplateViewModel>> ListTemplatesAsync(ClaimsPrincipal user)
    {
        if (!HasPermission("Apps.PermissionSets.View", "Apps.PermissionSets.Write"))
            return Array.Empty<ClientAppTemplateViewModel>();

        using var db = CreateDb();
        return await db.ClientAppTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new ClientAppTemplateViewModel
            {
                ClientAppTemplateId = t.ClientAppTemplateId,
                Name = t.Name
            }).ToListAsync();
    }

    /// <summary>
    /// Legt ein Rechtebuendel an - unter einem Template.
    /// </summary>
    /// <remarks>
    /// <b>Das Template ist Pflicht.</b> Bis PRE241 legte diese Methode mit <c>new TAppPermissionSet {
    /// Name = ... }</c> an, also mit <c>ClientAppTemplateId = 0</c>; seit PRE240 ist die Spalte ein
    /// Pflicht-Fremdschluessel, und der Insert scheiterte daran. Auf dieser Seite gab es damit gar keinen
    /// Weg, ein Buendel anzulegen, und im Kindgitter des Templates fehlte der Knopf ganz.
    /// </remarks>
    public async Task<PermissionSetViewModel?> CreateAsync(ClaimsPrincipal user, PermissionSetViewModel input)
    {
        if (!HasPermission("Apps.PermissionSets.Write"))
        {
            logger.LogWarning("Creating an app-permission-set was refused: Apps.PermissionSets.Write is missing.");
            return null;
        }

        using var db = CreateDb();
        if (!await TemplateExists(db, input.ClientAppTemplateId))
        {
            logger.LogError(
                "Creating the app-permission-set '{Name}' failed: app-template {TemplateId} does not exist.",
                input.Name, input.ClientAppTemplateId);
            return null;
        }

        if (await NameIsTaken(db, input.ClientAppTemplateId, input.Name, 0))
        {
            logger.LogError(
                "Creating the app-permission-set '{Name}' failed: app-template {TemplateId} already has a set of that name.",
                input.Name, input.ClientAppTemplateId);
            return null;
        }

        var entity = new TAppPermissionSet { Name = input.Name, ClientAppTemplateId = input.ClientAppTemplateId };
        db.AppPermissionSets.Add(entity);
        if (!await SaveOrLog(db, $"creating the app-permission-set '{input.Name}'"))
        {
            return null;
        }

        input.AppPermissionSetId = entity.AppPermissionSetId;
        return input;
    }

    /// <summary>
    /// Benennt ein Buendel um und kann es einem anderen Template zuordnen.
    /// </summary>
    /// <remarks>
    /// Der Wechsel des Templates ist bewusst erlaubt (ein falsch einsortiertes Buendel muss man
    /// verschieben koennen), <b>aber er verschiebt die Obergrenze</b>: fuehrt eine ClientApp dieses
    /// Buendel bereits, gehoerte es danach nicht mehr zum Template ihrer Anwendung. Deshalb wird der
    /// Wechsel verweigert, solange eine ClientApp es fuehrt - dieselbe Regel wie beim Loeschen.
    /// </remarks>
    public async Task<PermissionSetViewModel?> UpdateAsync(ClaimsPrincipal user, PermissionSetViewModel input)
    {
        if (!HasPermission("Apps.PermissionSets.Write"))
        {
            logger.LogWarning("Updating app-permission-set {SetId} was refused: Apps.PermissionSets.Write is missing.",
                input.AppPermissionSetId);
            return null;
        }

        using var db = CreateDb();
        var entity = await db.AppPermissionSets.FirstOrDefaultAsync(p => p.AppPermissionSetId == input.AppPermissionSetId);
        if (entity == null)
        {
            logger.LogWarning("App-permission-set {SetId} does not exist; nothing updated.", input.AppPermissionSetId);
            return null;
        }

        if (entity.ClientAppTemplateId != input.ClientAppTemplateId)
        {
            if (!await TemplateExists(db, input.ClientAppTemplateId))
            {
                logger.LogError(
                    "Moving app-permission-set {SetId} failed: app-template {TemplateId} does not exist.",
                    input.AppPermissionSetId, input.ClientAppTemplateId);
                return null;
            }

            if (await db.ClientAppPermissions.AnyAsync(cp => cp.AppPermissionSetId == input.AppPermissionSetId))
            {
                logger.LogError(
                    "Moving app-permission-set {SetId} to app-template {TemplateId} was refused: it is still granted to at least one client-app.",
                    input.AppPermissionSetId, input.ClientAppTemplateId);
                return null;
            }
        }

        if (await NameIsTaken(db, input.ClientAppTemplateId, input.Name, input.AppPermissionSetId))
        {
            logger.LogError(
                "Updating app-permission-set {SetId} failed: app-template {TemplateId} already has a set named '{Name}'.",
                input.AppPermissionSetId, input.ClientAppTemplateId, input.Name);
            return null;
        }

        entity.Name = input.Name;
        entity.ClientAppTemplateId = input.ClientAppTemplateId;
        return await SaveOrLog(db, $"updating the app-permission-set '{input.Name}'") ? input : null;
    }

    /// <summary>Ob es das Template ueberhaupt gibt.</summary>
    private static Task<bool> TemplateExists(TContext db, int clientAppTemplateId)
        => clientAppTemplateId <= 0
            ? Task.FromResult(false)
            : db.ClientAppTemplates.AnyAsync(t => t.ClientAppTemplateId == clientAppTemplateId);

    /// <summary>
    /// Ob das Template bereits ein Buendel dieses Namens fuehrt.
    /// </summary>
    /// <remarks>
    /// Vorab geprueft, damit der Fall als <b>Namenskonflikt</b> im Protokoll steht und nicht als
    /// anonymer Fremdschluessel-/Index-Fehler. Der Unique-Index <c>UQ_AppPermissionSetName</c>
    /// (ClientAppTemplateId, Name) bleibt die eigentliche Absicherung - diese Pruefung ersetzt ihn nicht,
    /// sie erklaert ihn nur.
    /// </remarks>
    private static Task<bool> NameIsTaken(TContext db, int clientAppTemplateId, string name, int exceptSetId)
        => db.AppPermissionSets.AnyAsync(p =>
            p.ClientAppTemplateId == clientAppTemplateId &&
            p.Name == name &&
            p.AppPermissionSetId != exceptSetId);

    /// <summary>
    /// Speichert und protokolliert den Fehlschlag mit Ursache.
    /// </summary>
    /// <remarks>
    /// Die Vorab-Pruefungen decken den erwarteten Fall ab; hier bleibt das Wettrennen (zwei Masken
    /// gleichzeitig) und alles Unerwartete. Ohne diese Stelle stuende in der Maske "Failed to create" und
    /// im Protokoll nichts.
    /// </remarks>
    private async Task<bool> SaveOrLog(TContext db, string what)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "The database refused {What}.", what);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int appPermissionSetId)
    {
        if (!HasPermission("Apps.PermissionSets.Write")) return false;
        using var db = CreateDb();
        var entity = await db.AppPermissionSets.FirstOrDefaultAsync(p => p.AppPermissionSetId == appPermissionSetId);
        if (entity == null) return false;
        db.AppPermissionSets.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AppPermissionAssignmentViewModel>> ListPermissionsForSetAsync(
        ClaimsPrincipal user, int appPermissionSetId, ListQuery query)
    {
        if (!HasPermission("Apps.PermissionSets.View", "Apps.PermissionSets.Write"))
            return new PagedResult<AppPermissionAssignmentViewModel>();

        using var db = CreateDb();
        var assignedPermissionIds = await db.AppPermissions
            .Where(ap => ap.AppPermissionSetId == appPermissionSetId)
            .Select(ap => ap.PermissionId)
            .ToListAsync();
        var assignedSet = new HashSet<int>(assignedPermissionIds);

        var q = db.Permissions.AsNoTracking().Where(p => p.TenantId == null);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(p => p.PermissionName.Contains(s));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.PermissionName).Page(p => p.PermissionId, query)
            .Select(p => new AppPermissionAssignmentViewModel
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName,
                Description = p.Description,
                AppPermissionSetId = appPermissionSetId,
                Assigned = false
            }).ToListAsync();
        foreach (var item in items)
        {
            item.Assigned = assignedSet.Contains(item.PermissionId);
        }
        return new PagedResult<AppPermissionAssignmentViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> SetPermissionForSetAsync(
        ClaimsPrincipal user, int appPermissionSetId, int permissionId, bool assigned)
    {
        if (!HasPermission("Apps.PermissionSets.Write")) return false;

        using var db = CreateDb();
        var existing = await db.AppPermissions.FirstOrDefaultAsync(ap =>
            ap.AppPermissionSetId == appPermissionSetId && ap.PermissionId == permissionId);

        if (assigned && existing == null)
        {
            db.AppPermissions.Add(new TAppPermission
            {
                AppPermissionSetId = appPermissionSetId,
                PermissionId = permissionId
            });
            await db.SaveChangesAsync();
            return true;
        }

        if (!assigned && existing != null)
        {
            db.AppPermissions.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }

        return true;
    }
}
