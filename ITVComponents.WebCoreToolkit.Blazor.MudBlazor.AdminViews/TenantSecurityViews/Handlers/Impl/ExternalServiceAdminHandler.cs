using System.Net.Http;
using System.Security.Claims;
using System.Text;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ITVComponents.WebCoreToolkit.Blazor.Paging;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;

public class ExternalServiceAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientApp,
    TClientAppPermission, TClientAppAccess, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IExternalServiceAdminHandler
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
    where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
    where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
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
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly IDbContextFactory<TContext> dbFactory;
    private readonly IServiceProvider services;
    private readonly ISecurityRepository secRepo;
    private readonly ILogger<IExternalServiceAdminHandler> logger;
    private readonly IOAuthHttpClientFactory? oauthClientFactory;

    public ExternalServiceAdminHandler(IDbContextFactory<TContext> dbFactory, IServiceProvider services, ISecurityRepository secRepo, ILogger<IExternalServiceAdminHandler> logger, IOAuthHttpClientFactory? oauthClientFactory = null)
    {
        this.dbFactory = dbFactory;
        this.services = services;
        this.secRepo = secRepo;
        this.logger = logger;
        this.oauthClientFactory = oauthClientFactory;
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

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { "Sysadmin" });

    /// <summary>
    /// The services the current user may see (<paramref name="forWrite"/> = false) or change (true). The context runs with
    /// ShowAllTenants, so this is the ONLY tenant-boundary of the handler: a sysadmin reaches every service, everybody else
    /// reads the services of the current tenant plus the global ones and changes only those of the current tenant.
    /// </summary>
    /// <param name="db">the per-operation context</param>
    /// <param name="forWrite">indicates whether the services are about to be changed</param>
    /// <returns>a query over the accessible services</returns>
    private IQueryable<TExternalOAuthService> AccessibleServices(TContext db, bool forWrite)
    {
        if (IsSysAdmin())
        {
            return db.ExternalOAuthServices;
        }

        // read before the query is built: the first read may query the database itself
        var currentTenantId = db.CurrentTenantId;
        if (currentTenantId == null)
        {
            // without a tenant, "TenantId == currentTenantId" would match the global services
            return db.ExternalOAuthServices.Where(n => false);
        }

        return forWrite
            ? db.ExternalOAuthServices.Where(n => n.TenantId == currentTenantId)
            : db.ExternalOAuthServices.Where(n => n.TenantId == currentTenantId || n.TenantId == null);
    }

    public async Task<PagedResult<ExternalOAuthServiceViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission("Services.Connections.View", "Services.Connections.Write"))
            return new PagedResult<ExternalOAuthServiceViewModel>();

        using var db = CreateDb();
        var q = AccessibleServices(db, false).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.UniqueConnectionName.Contains(s));
        }
        var total = await q.CountAsync();
        var sorted = query.SortDescending ? q.OrderByDescending(n => n.UniqueConnectionName) : q.OrderBy(n => n.UniqueConnectionName);
        var items = await sorted.Page(n => n.OAuthServiceId, query)
            .Select(n => new ExternalOAuthServiceViewModel
            {
                OAuthServiceId = n.OAuthServiceId,
                UniqueConnectionName = n.UniqueConnectionName,
                AuthorizationEndpoint = n.AuthorizationEndpoint,
                TokenEndpoint = n.TokenEndpoint,
                RevocationEndpoint = n.RevocationEndpoint,
                ClientId = n.ClientId,
                ClientSecret = null,
                Scope = n.Scope,
                Global = n.Global,
                AuthenticationType = n.AuthenticationType,
                TenantId = n.TenantId
            }).ToListAsync();
        return new PagedResult<ExternalOAuthServiceViewModel> { Items = items, TotalCount = total };
    }

    public async Task<ExternalOAuthServiceViewModel?> CreateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input)
    {
        if (!HasPermission("Services.Connections.Write")) return null;
        using var db = CreateDb();
        var sysAdmin = IsSysAdmin();
        if (!sysAdmin)
        {
            // a tenant-admin creates services for the own tenant only, whatever the input says
            input.Global = false;
            input.TenantId = db.CurrentTenantId;
            if (input.TenantId == null)
            {
                logger.LogWarning("External service '{ConnectionName}' was not created: there is no current tenant.", input.UniqueConnectionName);
                return null;
            }
        }
        else if (!input.Global && input.TenantId == null)
        {
            input.TenantId = db.CurrentTenantId;
        }

        var entity = new TExternalOAuthService
        {
            UniqueConnectionName = input.UniqueConnectionName,
            AuthorizationEndpoint = input.AuthorizationEndpoint ?? string.Empty,
            TokenEndpoint = input.TokenEndpoint ?? string.Empty,
            RevocationEndpoint = input.RevocationEndpoint ?? string.Empty,
            ClientId = input.ClientId ?? string.Empty,
            Scope = input.Scope ?? string.Empty,
            Global = input.Global,
            AuthenticationType = input.AuthenticationType,
            TenantId = input.Global ? null : input.TenantId
        };
        EncryptSecret(db, entity, input.ClientSecret);
        db.ExternalOAuthServices.Add(entity);
        await db.SaveChangesAsync();
        input.OAuthServiceId = entity.OAuthServiceId;
        input.ClientSecret = null;
        return input;
    }

    public async Task<ExternalOAuthServiceViewModel?> UpdateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input)
    {
        if (!HasPermission("Services.Connections.Write")) return null;
        using var db = CreateDb();
        var entity = await AccessibleServices(db, true).FirstOrDefaultAsync(n => n.OAuthServiceId == input.OAuthServiceId);
        if (entity == null)
        {
            logger.LogWarning("External service {OAuthServiceId} was not updated: it does not exist or is not editable for the current user.", input.OAuthServiceId);
            return null;
        }

        entity.UniqueConnectionName = input.UniqueConnectionName;
        entity.AuthorizationEndpoint = input.AuthorizationEndpoint ?? string.Empty;
        entity.TokenEndpoint = input.TokenEndpoint ?? string.Empty;
        entity.RevocationEndpoint = input.RevocationEndpoint ?? string.Empty;
        entity.ClientId = input.ClientId ?? string.Empty;
        entity.Scope = input.Scope ?? string.Empty;
        entity.AuthenticationType = input.AuthenticationType;

        EncryptSecret(db, entity, input.ClientSecret);
        await db.SaveChangesAsync();
        input.ClientSecret = null;
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int oauthServiceId)
    {
        if (!HasPermission("Services.Connections.Write")) return false;
        using var db = CreateDb();
        var entity = await AccessibleServices(db, true).FirstOrDefaultAsync(n => n.OAuthServiceId == oauthServiceId);
        if (entity == null)
        {
            logger.LogWarning("External service {OAuthServiceId} was not deleted: it does not exist or is not editable for the current user.", oauthServiceId);
            return false;
        }

        db.ExternalOAuthServices.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<ExternalOAuthServiceTenantLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, int oauthServiceId, ListQuery query)
    {
        if (!HasPermission("Services.Connections.View", "Services.Connections.Write"))
            return new PagedResult<ExternalOAuthServiceTenantLoginViewModel>();

        using var db = CreateDb();
        var sysAdmin = IsSysAdmin();
        var currentTenantId = db.CurrentTenantId;
        if (!await AccessibleServices(db, false).AnyAsync(n => n.OAuthServiceId == oauthServiceId))
        {
            logger.LogWarning("Tenant logins of external service {OAuthServiceId} were not listed: it does not exist or is not visible for the current user.", oauthServiceId);
            return new PagedResult<ExternalOAuthServiceTenantLoginViewModel>();
        }

        // a global service is used by many tenants - a tenant-admin only sees the login of the own tenant
        var q = from l in db.ExternalOAuthServiceTenantLogins.AsNoTracking()
                where l.OAuthServiceId == oauthServiceId && (sysAdmin || l.TenantId == currentTenantId)
                join t in db.Tenants.AsNoTracking() on l.TenantId equals t.TenantId into tj
                from tn in tj.DefaultIfEmpty()
                select new ExternalOAuthServiceTenantLoginViewModel
                {
                    ExternalOAuthServiceTenantLoginId = l.ExternalOAuthServiceTenantLoginId,
                    TenantId = l.TenantId,
                    TenantName = tn != null ? tn.DisplayName : null,
                    OAuthServiceId = l.OAuthServiceId,
                    Revoked = l.Revoked
                };
        var total = await q.CountAsync();
        var items = await q.OrderBy(x => x.TenantName).Page(x => x.TenantId, query)
            .ToListAsync();
        return new PagedResult<ExternalOAuthServiceTenantLoginViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> RevokeLoginAsync(ClaimsPrincipal user, int externalOAuthServiceTenantLoginId)
    {
        if (!HasPermission("Services.Connections.Write")) return false;
        using var db = CreateDb();
        var sysAdmin = IsSysAdmin();
        var currentTenantId = db.CurrentTenantId;
        var entity = await db.ExternalOAuthServiceTenantLogins.FirstOrDefaultAsync(n => n.ExternalOAuthServiceTenantLoginId == externalOAuthServiceTenantLoginId
                                                                                      && (sysAdmin || n.TenantId == currentTenantId));
        if (entity == null)
        {
            logger.LogWarning("Tenant login {LoginId} was not revoked: it does not exist or does not belong to the current tenant.", externalOAuthServiceTenantLoginId);
            return false;
        }

        entity.Revoked = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ExternalServiceDetailsViewModel?> GetDetailsAsync(ClaimsPrincipal user, int oauthServiceId)
    {
        if (!HasPermission("Services.Connections.View", "Services.Connections.Write")) return null;
        using var db = CreateDb();
        // no lazy loading on untracked entities - the parameters are included explicitly
        var entity = await AccessibleServices(db, false).AsNoTracking().Include(n => n.TokenParameters)
            .FirstOrDefaultAsync(n => n.OAuthServiceId == oauthServiceId);
        if (entity == null)
        {
            logger.LogWarning("Details of external service {OAuthServiceId} are unavailable: it does not exist or is not visible for the current user.", oauthServiceId);
            return null;
        }

        var serviceDef = entity.ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>(true);
        var isConnected = entity.AuthenticationType != ExternalServiceAuthenticationType.OAuthAuthorizationFlow
            || await db.ExternalOAuthServiceTenantLogins.AnyAsync(l => l.OAuthServiceId == oauthServiceId && !l.Revoked);

        return new ExternalServiceDetailsViewModel
        {
            OAuthServiceId = entity.OAuthServiceId,
            UniqueConnectionName = entity.UniqueConnectionName,
            GlobalUniqueConnectionName = serviceDef.GlobalUniqueConnectionName,
            AuthenticationType = entity.AuthenticationType,
            AuthorizationEndpoint = entity.AuthorizationEndpoint,
            TokenEndpoint = entity.TokenEndpoint,
            RevocationEndpoint = entity.RevocationEndpoint,
            ClientId = entity.ClientId,
            Scope = entity.Scope,
            Global = entity.Global,
            RedirectUri = serviceDef.RedirectUri(),
            IsConnected = isConnected,
            TokenRequestParameters = serviceDef.TokenRequestParameters
        };
    }

    public async Task<ExternalServiceTestResultViewModel> PerformTestAsync(ClaimsPrincipal user, ExternalServiceTestRequestViewModel request)
    {
        if (!HasPermission("Services.Connections.View", "Services.Connections.Write"))
            return new ExternalServiceTestResultViewModel { ErrorMessage = "Insufficient permission", IsSuccess = false };
        if (oauthClientFactory == null)
            return new ExternalServiceTestResultViewModel { ErrorMessage = "OAuth client factory is not registered", IsSuccess = false };

        string globalConnectionName;
        using (var db = CreateDb())
        {
            var entity = await AccessibleServices(db, false).AsNoTracking().FirstOrDefaultAsync(n => n.OAuthServiceId == request.OAuthServiceId);
            if (entity == null)
                return new ExternalServiceTestResultViewModel { ErrorMessage = "Service not found", IsSuccess = false };

            globalConnectionName = entity.ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>(true).GlobalUniqueConnectionName;
        }

        // the context is gone before the request starts - the client resolves the service on its own
        try
        {
            using var client = oauthClientFactory.Create(globalConnectionName);
            var httpMethod = new HttpMethod(request.Verb.ToUpperInvariant());
            using var message = new HttpRequestMessage(httpMethod, request.TargetUrl);
            if (!string.IsNullOrEmpty(request.HttpActionBody))
            {
                message.Content = new StringContent(request.HttpActionBody, new UTF8Encoding(), request.ActionBodyContentType);
            }
            if (!string.IsNullOrEmpty(request.CustomHeaders))
            {
                foreach (var h in request.CustomHeaders.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var idx = h.IndexOf(':');
                    if (idx <= 0) continue;
                    message.Headers.TryAddWithoutValidation(h.Substring(0, idx).Trim(), h.Substring(idx + 1).Trim());
                }
            }

            using var result = await client.SendAsync(message);
            var body = await result.Content.ReadAsStringAsync();
            return new ExternalServiceTestResultViewModel
            {
                StatusCode = (int)result.StatusCode,
                Content = body,
                IsSuccess = result.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Test request against external service {ConnectionName} failed.", globalConnectionName);
            return new ExternalServiceTestResultViewModel { ErrorMessage = ex.Message, IsSuccess = false };
        }
    }

    public async Task<PagedResult<ExternalOAuthServiceParameterViewModel>> ListParametersAsync(ClaimsPrincipal user, int oauthServiceId, ListQuery query)
    {
        if (!HasPermission("Services.Connections.View", "Services.Connections.Write"))
            return new PagedResult<ExternalOAuthServiceParameterViewModel>();

        using var db = CreateDb();
        // the parameters have no tenant-filter of their own; they are only reachable through an accessible service
        var q = AccessibleServices(db, false).AsNoTracking()
            .Where(n => n.OAuthServiceId == oauthServiceId)
            .SelectMany(n => n.TokenParameters);
        var total = await q.CountAsync();
        var items = await q.OrderBy(n => n.ParameterName).Page(n => n.OAuthServiceParameterId, query)
            .Select(n => new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceParameterId = n.OAuthServiceParameterId,
                OAuthServiceId = n.OAuthServiceId,
                ParameterName = n.ParameterName,
                // Ein Geheimnis verlaesst die Datenbank nicht wieder - auch nicht verschluesselt.
                ParameterValue = n.IsSecret ? null : n.ParameterValue,
                IsSecret = n.IsSecret,
                HasValue = !string.IsNullOrEmpty(n.ParameterValue)
            }).ToListAsync();
        return new PagedResult<ExternalOAuthServiceParameterViewModel> { Items = items, TotalCount = total };
    }

    public async Task<ExternalOAuthServiceParameterSaveResult> SaveParameterAsync(ClaimsPrincipal user, ExternalOAuthServiceParameterViewModel input)
    {
        if (!HasPermission("Services.Connections.Write"))
            return ExternalOAuthServiceParameterSaveResult.Failed("Insufficient permission.");

        using var db = CreateDb();
        var service = await AccessibleServices(db, true).Include(n => n.TokenParameters)
            .FirstOrDefaultAsync(n => n.OAuthServiceId == input.OAuthServiceId);
        if (service == null)
        {
            logger.LogWarning("Token parameter of external service {OAuthServiceId} was not saved: the service does not exist or is not editable for the current user.", input.OAuthServiceId);
            return ExternalOAuthServiceParameterSaveResult.Failed("The parameters of this service can not be edited.");
        }

        var name = input.ParameterName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ExternalOAuthServiceParameterSaveResult.Failed("A parameter name is required.");
        }

        if (TokenRequestParameterRules.IsReserved(service.AuthenticationType, name))
        {
            return ExternalOAuthServiceParameterSaveResult.Failed($"The parameter '{name}' is set by the authentication type itself and can not be overridden.");
        }

        if (service.TokenParameters.Any(n => n.OAuthServiceParameterId != input.OAuthServiceParameterId && string.Equals(n.ParameterName, name, StringComparison.OrdinalIgnoreCase)))
        {
            return ExternalOAuthServiceParameterSaveResult.Failed($"A parameter named '{name}' already exists.");
        }

        ExternalOAuthServiceParameter? model;
        if (input.OAuthServiceParameterId == 0)
        {
            model = new ExternalOAuthServiceParameter();
            service.TokenParameters.Add(model);
        }
        else
        {
            model = service.TokenParameters.FirstOrDefault(n => n.OAuthServiceParameterId == input.OAuthServiceParameterId);
            if (model == null)
            {
                logger.LogWarning("Token parameter {ParameterId} was not updated: it does not belong to external service {OAuthServiceId}.", input.OAuthServiceParameterId, input.OAuthServiceId);
                return ExternalOAuthServiceParameterSaveResult.Failed("The parameter does not exist (anymore).");
            }
        }

        model.ParameterName = name;
        switch (model.ApplyValue(input.IsSecret, input.ParameterValue!, plain => EncryptFor(db, service, plain)))
        {
            case TokenParameterValueResult.SecretMustBeEnteredAgain:
                return ExternalOAuthServiceParameterSaveResult.Failed("Enter the value again: a secret is not turned back into plain text.");
            case TokenParameterValueResult.EncryptionFailed:
                // EncryptFor hat den Grund schon protokolliert.
                return ExternalOAuthServiceParameterSaveResult.Failed("The value could not be encrypted, so it was not stored.");
        }

        await db.SaveChangesAsync();
        return ExternalOAuthServiceParameterSaveResult.Saved(new ExternalOAuthServiceParameterViewModel
        {
            OAuthServiceParameterId = model.OAuthServiceParameterId,
            OAuthServiceId = service.OAuthServiceId,
            ParameterName = model.ParameterName,
            ParameterValue = model.IsSecret ? null : model.ParameterValue,
            IsSecret = model.IsSecret,
            HasValue = !string.IsNullOrEmpty(model.ParameterValue)
        });
    }

    public async Task<bool> DeleteParameterAsync(ClaimsPrincipal user, int oauthServiceId, int oauthServiceParameterId)
    {
        if (!HasPermission("Services.Connections.Write")) return false;
        using var db = CreateDb();
        var service = await AccessibleServices(db, true).Include(n => n.TokenParameters)
            .FirstOrDefaultAsync(n => n.OAuthServiceId == oauthServiceId);
        var model = service?.TokenParameters.FirstOrDefault(n => n.OAuthServiceParameterId == oauthServiceParameterId);
        if (model == null)
        {
            logger.LogWarning("Token parameter {ParameterId} of external service {OAuthServiceId} was not deleted: it does not exist or is not editable for the current user.", oauthServiceParameterId, oauthServiceId);
            return false;
        }

        // required relationship: removing the parameter from the collection deletes the orphaned row
        service!.TokenParameters.Remove(model);
        await db.SaveChangesAsync();
        return true;
    }

    private void EncryptSecret(TContext db, TExternalOAuthService entity, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || !secret.StartsWith("encrypt:")) return;
        var encrypted = EncryptFor(db, entity, secret.Substring(8));
        if (encrypted != null)
        {
            entity.ClientSecret = encrypted;
        }
    }

    /// <summary>
    /// Verschluesselt mit dem Schluessel des Dienstes: global mit dem Systemschluessel, sonst mit dem des Mandanten -
    /// fuer ClientSecret und geheime Token-Parameter gleich, weil beide an derselben Stelle entschluesselt werden.
    /// </summary>
    /// <returns>der verschluesselte Wert, oder null, wenn der Mandant nicht gefunden wurde (protokolliert)</returns>
    private string? EncryptFor(TContext db, TExternalOAuthService entity, string plain)
    {
        if (entity.Global)
        {
            return plain.Encrypt();
        }

        var t = db.Tenants.FirstOrDefault(n => n.TenantId == entity.TenantId);
        if (t == null)
        {
            logger.LogError("A secret of external service '{ConnectionName}' was not stored: tenant {TenantId} was not found.", entity.UniqueConnectionName, entity.TenantId);
            return null;
        }

        return secRepo.Encrypt(plain, t.TenantName);
    }
}
