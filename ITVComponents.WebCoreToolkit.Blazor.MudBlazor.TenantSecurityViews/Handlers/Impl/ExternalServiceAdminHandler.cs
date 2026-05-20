using System.Net.Http;
using System.Security.Claims;
using System.Text;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.TenantSecurityViews.Blazor.Handlers.Impl;

public class ExternalServiceAdminHandler<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission,
    TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu,
    TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization,
    TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant,
    TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter,
    TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp,
    TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter,
    TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService,
    TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : IExternalServiceAdminHandler
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
    where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
    where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
{
    private readonly TContext db;
    private readonly IServiceProvider services;
    private readonly ISecurityRepository secRepo;
    private readonly IOAuthHttpClientFactory? oauthClientFactory;

    public ExternalServiceAdminHandler(TContext db, IServiceProvider services, ISecurityRepository secRepo, IOAuthHttpClientFactory? oauthClientFactory = null)
    {
        this.db = db;
        this.services = services;
        this.secRepo = secRepo;
        this.oauthClientFactory = oauthClientFactory;
        this.db.ShowAllTenants = true;
        this.db.HideGlobals = false;
    }

    public bool HasPermission(ClaimsPrincipal user, params string[] permissions)
        => services.VerifyUserPermissions(permissions);

    private bool IsSysAdmin() => services.VerifyUserPermissions(new[] { "Sysadmin" });

    public async Task<PagedResult<ExternalOAuthServiceViewModel>> ListAsync(ClaimsPrincipal user, ListQuery query)
    {
        if (!HasPermission(user, "Services.Connections.View", "Services.Connections.Write"))
            return new PagedResult<ExternalOAuthServiceViewModel>();

        var sysAdmin = IsSysAdmin();
        var currentTenantId = db.CurrentTenantId;
        IQueryable<TExternalOAuthService> q;
        if (sysAdmin)
        {
            q = db.ExternalOAuthServices.AsNoTracking();
        }
        else
        {
            q = db.ExternalOAuthServices.AsNoTracking()
                .Where(n => n.TenantId == currentTenantId || n.TenantId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(n => n.UniqueConnectionName.Contains(s));
        }
        var total = await q.CountAsync();
        q = query.SortDescending ? q.OrderByDescending(n => n.UniqueConnectionName) : q.OrderBy(n => n.UniqueConnectionName);
        var items = await q.Skip(query.Page * query.PageSize).Take(query.PageSize)
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
        if (!HasPermission(user, "Services.Connections.Write")) return null;
        var sysAdmin = IsSysAdmin();
        if (!sysAdmin) input.Global = false;
        if (!input.Global && input.TenantId == null) input.TenantId = db.CurrentTenantId;

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
        EncryptSecret(entity, input.ClientSecret);
        db.ExternalOAuthServices.Add(entity);
        await db.SaveChangesAsync();
        input.OAuthServiceId = entity.OAuthServiceId;
        input.ClientSecret = null;
        return input;
    }

    public async Task<ExternalOAuthServiceViewModel?> UpdateAsync(ClaimsPrincipal user, ExternalOAuthServiceViewModel input)
    {
        if (!HasPermission(user, "Services.Connections.Write")) return null;
        var entity = await db.ExternalOAuthServices.FirstOrDefaultAsync(n => n.OAuthServiceId == input.OAuthServiceId);
        if (entity == null) return null;

        entity.UniqueConnectionName = input.UniqueConnectionName;
        entity.AuthorizationEndpoint = input.AuthorizationEndpoint ?? string.Empty;
        entity.TokenEndpoint = input.TokenEndpoint ?? string.Empty;
        entity.RevocationEndpoint = input.RevocationEndpoint ?? string.Empty;
        entity.ClientId = input.ClientId ?? string.Empty;
        entity.Scope = input.Scope ?? string.Empty;
        entity.AuthenticationType = input.AuthenticationType;

        EncryptSecret(entity, input.ClientSecret);
        await db.SaveChangesAsync();
        input.ClientSecret = null;
        return input;
    }

    public async Task<bool> DeleteAsync(ClaimsPrincipal user, int oauthServiceId)
    {
        if (!HasPermission(user, "Services.Connections.Write")) return false;
        var entity = await db.ExternalOAuthServices.FirstOrDefaultAsync(n => n.OAuthServiceId == oauthServiceId);
        if (entity == null) return false;
        db.ExternalOAuthServices.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<PagedResult<ExternalOAuthServiceTenantLoginViewModel>> ListLoginsAsync(ClaimsPrincipal user, int oauthServiceId, ListQuery query)
    {
        if (!HasPermission(user, "Services.Connections.View", "Services.Connections.Write"))
            return new PagedResult<ExternalOAuthServiceTenantLoginViewModel>();

        var q = from l in db.ExternalOAuthServiceTenantLogins.AsNoTracking()
                where l.OAuthServiceId == oauthServiceId
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
        var items = await q.OrderBy(x => x.TenantName)
            .Skip(query.Page * query.PageSize).Take(query.PageSize)
            .ToListAsync();
        return new PagedResult<ExternalOAuthServiceTenantLoginViewModel> { Items = items, TotalCount = total };
    }

    public async Task<bool> RevokeLoginAsync(ClaimsPrincipal user, int externalOAuthServiceTenantLoginId)
    {
        if (!HasPermission(user, "Services.Connections.Write")) return false;
        var entity = await db.ExternalOAuthServiceTenantLogins.FirstOrDefaultAsync(n => n.ExternalOAuthServiceTenantLoginId == externalOAuthServiceTenantLoginId);
        if (entity == null) return false;
        entity.Revoked = true;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ExternalServiceDetailsViewModel?> GetDetailsAsync(ClaimsPrincipal user, int oauthServiceId)
    {
        if (!HasPermission(user, "Services.Connections.View", "Services.Connections.Write")) return null;
        var entity = await db.ExternalOAuthServices.AsNoTracking().FirstOrDefaultAsync(n => n.OAuthServiceId == oauthServiceId);
        if (entity == null) return null;

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
            IsConnected = isConnected
        };
    }

    public async Task<ExternalServiceTestResultViewModel> PerformTestAsync(ClaimsPrincipal user, ExternalServiceTestRequestViewModel request)
    {
        if (!HasPermission(user, "Services.Connections.View", "Services.Connections.Write"))
            return new ExternalServiceTestResultViewModel { ErrorMessage = "Insufficient permission", IsSuccess = false };
        if (oauthClientFactory == null)
            return new ExternalServiceTestResultViewModel { ErrorMessage = "OAuth client factory is not registered", IsSuccess = false };

        var entity = await db.ExternalOAuthServices.AsNoTracking().FirstOrDefaultAsync(n => n.OAuthServiceId == request.OAuthServiceId);
        if (entity == null)
            return new ExternalServiceTestResultViewModel { ErrorMessage = "Service not found", IsSuccess = false };

        var serviceDef = entity.ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>(true);

        try
        {
            using var client = oauthClientFactory.Create(serviceDef.GlobalUniqueConnectionName);
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
            return new ExternalServiceTestResultViewModel { ErrorMessage = ex.Message, IsSuccess = false };
        }
    }

    private void EncryptSecret(TExternalOAuthService entity, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || !secret.StartsWith("encrypt:")) return;
        var plain = secret.Substring(8);
        if (entity.Global)
        {
            entity.ClientSecret = plain.Encrypt();
        }
        else
        {
            var t = db.Tenants.FirstOrDefault(n => n.TenantId == entity.TenantId);
            if (t != null) entity.ClientSecret = secRepo.Encrypt(plain, t.TenantName);
        }
    }
}
