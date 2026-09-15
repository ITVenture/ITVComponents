using System.Security.Claims;
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

public class AppTemplateAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp,
    TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IAppTemplateAdminHandler
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

    public AppTemplateAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services)
    {
        this.dbFactory = dbFactory;
        this.services = services;
        // Ueber die Factory und mit festem Kategorienamen statt als Konstruktor-Parameter: die Signatur
        // ist an mehreren Stellen verdrahtet, und ein typeof() auf diesen Typ muesste vierzig Kommata
        // exakt treffen - das bricht beim naechsten Typparameter still.
        logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AppTemplateAdminHandler");
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

    public async Task<PagedResult<ClientAppTemplateViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("Apps.Templates.View", "Apps.Templates.Write"))
            return new PagedResult<ClientAppTemplateViewModel>();

        using var db = CreateDb();
        var q = db.ClientAppTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.Name.Contains(s));
        }
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(t => t.Name) : q.OrderBy(t => t.Name);
        var items = await sorted.Page(t => t.ClientAppTemplateId, query)
            .Select(t => new ClientAppTemplateViewModel
            {
                ClientAppTemplateId = t.ClientAppTemplateId,
                Name = t.Name
            }).ToListAsync();
        return new PagedResult<ClientAppTemplateViewModel> { Items = items, TotalCount = total };
    }

    public async Task<ClientAppTemplateViewModel?> CreateAsync(ClaimsPrincipal user, ClientAppTemplateViewModel input)
    {
        if (!HasPermission("Apps.PermissionSets.Write")) return null;
        using var db = CreateDb();
        var entity = new TClientAppTemplate { Name = input.Name };
        db.ClientAppTemplates.Add(entity);
        await db.SaveChangesAsync();
        input.ClientAppTemplateId = entity.ClientAppTemplateId;
        return input;
    }

    public async Task<ClientAppTemplateViewModel?> UpdateAsync(ClaimsPrincipal user, ClientAppTemplateViewModel input)
    {
        if (!HasPermission("Apps.PermissionSets.Write")) return null;
        using var db = CreateDb();
        var entity = await db.ClientAppTemplates.FirstOrDefaultAsync(t => t.ClientAppTemplateId == input.ClientAppTemplateId);
        if (entity == null) return null;
        entity.Name = input.Name;
        await db.SaveChangesAsync();
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int clientAppTemplateId)
    {
        if (!HasPermission("Apps.PermissionSets.Write")) return false;
        using var db = CreateDb();
        var entity = await db.ClientAppTemplates.FirstOrDefaultAsync(t => t.ClientAppTemplateId == clientAppTemplateId);
        if (entity == null) return false;
        db.ClientAppTemplates.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<AppPermissionSetAssignmentViewModel>> ListPermissionSetsForTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, ListQuery query)
    {
        if (!HasPermission("Apps.Templates.View", "Apps.Templates.Write"))
            return new PagedResult<AppPermissionSetAssignmentViewModel>();

        using var db = CreateDb();

        // Die Buendel gehoeren jetzt DIREKT zum Template (AppPermissionSet.ClientAppTemplateId); es gibt
        // keine global geteilten Buendel mehr, die man einem Template zuordnet oder entzieht. Was diese
        // Liste zeigt, gehoert dem Template - deshalb ist "Assigned" durchgehend true und bleibt nur
        // erhalten, bis die Maske in Phase 5 auf das neue Modell umgebaut ist.
        var q = db.AppPermissionSets.AsNoTracking()
            .Where(ps => ps.ClientAppTemplateId == clientAppTemplateId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(ps => ps.Name.Contains(s));
        }

        var total = await q.CountAsync();
        var items = await q.OrderBy(ps => ps.Name).Page(ps => ps.AppPermissionSetId, query)
            .Select(ps => new AppPermissionSetAssignmentViewModel
            {
                AppPermissionSetId = ps.AppPermissionSetId,
                PermissionSetName = ps.Name,
                ClientAppTemplateId = clientAppTemplateId,
                Assigned = true
            }).ToListAsync();
        return new PagedResult<AppPermissionSetAssignmentViewModel> { Items = items, TotalCount = total };
    }

    /// <summary>
    /// Legt ein Rechtebuendel unter diesem Template an.
    /// </summary>
    /// <remarks>
    /// Der Weg, der bis PRE241 fehlte: das Kindgitter konnte auflisten und loeschen, aber nicht anlegen -
    /// und der Weg ueber die Buendel-Seite war seinerseits kaputt, weil er das Template nicht setzte.
    /// Es gab damit gar keine Moeglichkeit, ein Buendel anzulegen.
    /// </remarks>
    public async Task<AppPermissionSetAssignmentViewModel?> CreatePermissionSetForTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, string name)
    {
        if (!HasPermission("Apps.PermissionSets.Write"))
        {
            logger.LogWarning(
                "Creating a permission-set below app-template {TemplateId} was refused: Apps.PermissionSets.Write is missing.",
                clientAppTemplateId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            logger.LogWarning("Creating a permission-set below app-template {TemplateId} failed: the name is empty.",
                clientAppTemplateId);
            return null;
        }

        name = name.Trim();
        using var db = CreateDb();
        if (!await db.ClientAppTemplates.AnyAsync(t => t.ClientAppTemplateId == clientAppTemplateId))
        {
            logger.LogError("Creating the permission-set '{Name}' failed: app-template {TemplateId} does not exist.",
                name, clientAppTemplateId);
            return null;
        }

        // Vorab geprueft, damit der Fall als Namenskonflikt im Protokoll steht und nicht als anonymer
        // Index-Fehler. UQ_AppPermissionSetName (ClientAppTemplateId, Name) bleibt die Absicherung.
        if (await db.AppPermissionSets.AnyAsync(ps =>
                ps.ClientAppTemplateId == clientAppTemplateId && ps.Name == name))
        {
            logger.LogError(
                "Creating the permission-set '{Name}' failed: app-template {TemplateId} already has a set of that name.",
                name, clientAppTemplateId);
            return null;
        }

        var entity = new TAppPermissionSet { Name = name, ClientAppTemplateId = clientAppTemplateId };
        db.AppPermissionSets.Add(entity);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Bleibt das Wettrennen zweier Masken und alles Unerwartete - ohne diese Stelle stuende in
            // der Maske "Failed to create" und im Protokoll nichts.
            logger.LogError(ex, "The database refused creating the permission-set '{Name}' below app-template {TemplateId}.",
                name, clientAppTemplateId);
            return null;
        }

        return new AppPermissionSetAssignmentViewModel
        {
            AppPermissionSetId = entity.AppPermissionSetId,
            PermissionSetName = entity.Name,
            ClientAppTemplateId = clientAppTemplateId,
            Assigned = true
        };
    }

    /// <summary>
    /// Benennt ein Rechtebuendel dieses Templates um.
    /// </summary>
    /// <remarks>
    /// Nur der Name: das Template zu wechseln verschoebe die Obergrenze einer bereits ausgestatteten
    /// Anwendung und gehoert deshalb nicht in das Kindgitter des Templates, in dem man gerade steht.
    /// </remarks>
    public async Task<bool> RenamePermissionSetOfTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, int appPermissionSetId, string name)
    {
        if (!HasPermission("Apps.PermissionSets.Write"))
        {
            logger.LogWarning(
                "Renaming permission-set {SetId} of app-template {TemplateId} was refused: Apps.PermissionSets.Write is missing.",
                appPermissionSetId, clientAppTemplateId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            logger.LogWarning("Renaming permission-set {SetId} failed: the name is empty.", appPermissionSetId);
            return false;
        }

        name = name.Trim();
        using var db = CreateDb();
        var entity = await db.AppPermissionSets.FirstOrDefaultAsync(ps =>
            ps.AppPermissionSetId == appPermissionSetId && ps.ClientAppTemplateId == clientAppTemplateId);
        if (entity == null)
        {
            logger.LogWarning("Permission-set {SetId} does not exist below app-template {TemplateId}; nothing renamed.",
                appPermissionSetId, clientAppTemplateId);
            return false;
        }

        if (await db.AppPermissionSets.AnyAsync(ps =>
                ps.ClientAppTemplateId == clientAppTemplateId && ps.Name == name &&
                ps.AppPermissionSetId != appPermissionSetId))
        {
            logger.LogError(
                "Renaming permission-set {SetId} failed: app-template {TemplateId} already has a set named '{Name}'.",
                appPermissionSetId, clientAppTemplateId, name);
            return false;
        }

        entity.Name = name;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "The database refused renaming permission-set {SetId} to '{Name}'.",
                appPermissionSetId, name);
            return false;
        }
    }

    /// <summary>
    /// Loescht ein Rechtebuendel dieses Templates.
    /// </summary>
    /// <remarks>
    /// Das ersetzt das fruehere "Zuordnung entziehen": ein Buendel gehoert genau einem Template, es gibt
    /// also nichts mehr zu entziehen - es wird geloescht. Der Name sagt das ausdruecklich, damit niemand
    /// die alte, harmlosere Bedeutung hineinliest.
    /// <para>
    /// Verweigert wird, solange eine ClientApp das Buendel noch fuehrt: sonst verloere eine laufende
    /// Anwendung stillschweigend ihre Rechte.
    /// </para>
    /// </remarks>
    public async Task<bool> DeletePermissionSetFromTemplateAsync(
        ClaimsPrincipal user, int clientAppTemplateId, int appPermissionSetId)
    {
        if (!HasPermission("Apps.PermissionSets.Write"))
        {
            logger.LogWarning(
                "Deleting permission-set {SetId} of app-template {TemplateId} was refused: Apps.PermissionSets.Write is missing.",
                appPermissionSetId, clientAppTemplateId);
            return false;
        }

        using var db = CreateDb();
        var entity = await db.AppPermissionSets.FirstOrDefaultAsync(ps =>
            ps.AppPermissionSetId == appPermissionSetId && ps.ClientAppTemplateId == clientAppTemplateId);
        if (entity == null)
        {
            logger.LogWarning(
                "Permission-set {SetId} does not exist below app-template {TemplateId}; nothing deleted.",
                appPermissionSetId, clientAppTemplateId);
            return false;
        }

        var stillInUse = await db.ClientAppPermissions.AnyAsync(cp => cp.AppPermissionSetId == appPermissionSetId);
        if (stillInUse)
        {
            logger.LogWarning(
                "Permission-set {SetId} of app-template {TemplateId} is still granted to at least one client-app; not deleted.",
                appPermissionSetId, clientAppTemplateId);
            return false;
        }

        db.AppPermissionSets.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }
}
