using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.SharedAssets
{
    public abstract class SharedAssetInfoProvider<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig, TContext> : ISharedAssetAdapter
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
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TContext: DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>
        where TTenant : Tenant
        where TWebPlugin:WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant:WebPluginConstant<TTenant>
        where TWebPluginGenericParameter:WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence:Sequence<TTenant>
        where TTenantSetting:TenantSetting<TTenant>
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
        public const string AnonymousTag = "##ANONYMOUS";
        private readonly IUserNameMapper userNameMapper;
        private readonly ISecurityRepository securityRepo;
        private readonly IToolkitContextFactory contextFactory;
        private readonly ISecurityAccessProvider securityAccessProvider;
        private readonly IServiceProvider services;
        private object sync = new();
        private int impersonationDeactivated = 0;

        public SharedAssetInfoProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, IToolkitContextFactory contextFactory, ISecurityAccessProvider securityAccessProvider, IServiceProvider services)
        {
            this.userNameMapper = userNameMapper;
            this.securityRepo = securityRepo;
            this.contextFactory = contextFactory;
            this.securityAccessProvider = securityAccessProvider;
            this.services = services;
        }

        protected bool ImpersonationDeactivated => impersonationDeactivated != 0;

        public AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false)
        {
            if (!ImpersonationDeactivated)
            {
                using var lease = contextFactory.Lease<TContext>();
                var database = lease.Context;
                var labels = requestor.Identities.Where(n => n.IsAuthenticated).Select(t => new IdentityInfo
                    { Labels = userNameMapper.GetUserLabels(t), AuthenticationType = t.AuthenticationType }).ToArray();
                var tenants = labels.SelectMany(i =>
                        securityRepo.GetEligibleScopes(i.Labels, i.AuthenticationType).Select(n => n.ScopeName))
                    .Distinct()
                    .ToArray();
                bool accessible = AssetIsAccessible(database, assetKey, labels, tenants, out var asset);
                AssetInfo retVal;
                bool hasOwnerPrivileges = false;
                if (asOwner && asset != null)
                {
                    hasOwnerPrivileges = (asset.Template.RequiredFeature == null ||
                                          services.VerifyActivatedFeatures(
                                              new[] { asset.Template.RequiredFeature.FeatureName }, out _)) &&
                                         (asset.Template.RequiredPermission == null ||
                                          services.VerifyUserPermissions(
                                              new[] { asset.Template.RequiredPermission.PermissionName }, out _));
                }

                if (hasOwnerPrivileges)
                {
                    var f = new FullAssetInfo
                    {
                        NotBefore = asset.NotBefore,
                        NotAfter = asset.NotAfter,
                        AnonymousAccessTokenRaw = asset.AnonymousAccessTokenRaw
                    };

                    retVal = f;
                    f.UserScopeShares.AddRange(asset.TenantFilters.Select(n => n.LabelFilter));
                    f.UserShares.AddRange(asset.UserFilters.Select(n => n.LabelFilter));
                }
                else
                {
                    retVal = new AssetInfo();
                }

                if (accessible)
                {
                    retVal.AssetKey = asset.AssetKey;
                    retVal.AssetTitle = asset.AssetTitle;
                    retVal.Features = asset.Template.FeatureGrants.Select(n => n.Feature.FeatureName).ToArray();
                    retVal.Permissions = asset.Template.Grants.Select(n => n.Permission.PermissionName).ToArray();
                    retVal.UserScopeName = asset.AssetOwner.TenantName;
                    retVal.AssetRootPath = asset.RootPath;
                    // Worauf die Freigabe zeigt, reist von hier aus mit: der Riegel im Endpunkt vergleicht
                    // spaeter gegen genau diese Werte.
                    retVal.Arguments = ReadArguments(database, asset.AssetTemplateId);
                    retVal.Values = AssetArgumentValues.FromJson(asset.ArgumentValuesJson);
                    retVal.Enforcement = asset.Template.ArgumentEnforcement;
                    retVal.AuditMode = asset.Template.AuditMode;
                    retVal.TemplateSystemKey = asset.Template.SystemKey;
                    retVal.RecipientLabel = asset.RecipientLabel;
                    return retVal;
                }
            }

            return null;
        }

        public bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor)
        {
            requestPath = Canonical(requestPath);
            if (!ImpersonationDeactivated)
            {
                using var lease = contextFactory.Lease<TContext>();
                var database = lease.Context;
                var labels = requestor.Identities.Where(n => n.IsAuthenticated).Select(t => new IdentityInfo
                    { Labels = userNameMapper.GetUserLabels(t), AuthenticationType = t.AuthenticationType }).ToArray();
                var tenants = labels.SelectMany(i =>
                        securityRepo.GetEligibleScopes(i.Labels, i.AuthenticationType).Select(n => n.ScopeName))
                    .Distinct()
                    .ToArray();
                bool retVal = AssetIsAccessible(database, assetKey, labels, tenants, out var asset);
                if (retVal)
                {
                    retVal &= IsTemplateValidForPath(asset.Template, requestPath);
                }

                return retVal;
            }

            return false;
        }

        public AssetTemplateInfo[] GetEligibleShares(string requestPath)
        {
            requestPath = Canonical(requestPath);
            if (!ImpersonationDeactivated)
            {
                using var lease = contextFactory.Lease<TContext>();
                var database = lease.Context;
                var tmp = database.AssetTemplates.ToArray().Where(n =>
                    (n.RequiredFeature == null ||
                     services.VerifyActivatedFeatures(new[] { n.RequiredFeature.FeatureName }, out _)) &&
                    (n.RequiredPermission == null ||
                     services.VerifyUserPermissions(new[] { n.RequiredPermission.PermissionName }, out _))).ToArray();
                var retVal = new List<AssetTemplateInfo>();
                foreach (var template in tmp)
                {
                    if (IsTemplateValidForPath(template, requestPath))
                    {
                        retVal.Add(new AssetTemplateInfo
                        {
                            AssetTemplateTitle = template.Name,
                            TemplateKey = template.SystemKey,
                            Arguments = ReadArguments(database, template.AssetTemplateId)
                        });
                    }
                }

                return retVal.ToArray();
            }

            return Array.Empty<AssetTemplateInfo>();
        }

        public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title)
            => CreateSharedAsset(requestPath, template, title, null, null, out _);

        public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
            IDictionary<string, string> argumentValues, string recipientLabel, out string error)
        {
            error = null;
            requestPath = Canonical(requestPath);
            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            var assetTmp = database.AssetTemplates.First(n => n.SystemKey == template.TemplateKey);
            var ok = (assetTmp.RequiredFeature == null ||
                      services.VerifyActivatedFeatures(new[] { assetTmp.RequiredFeature.FeatureName }, out _)) &&
                     (assetTmp.RequiredPermission == null ||
                      services.VerifyUserPermissions(new[] { assetTmp.RequiredPermission.PermissionName }, out _));
            if (ok && database.CurrentTenantId != null && IsTemplateValidForPath(assetTmp, requestPath))
            {
                // Die harte Pruefung: sie braucht nur die Vorlage. Fehlt ein Pflichtargument oder passt ein
                // Wert nicht zu seinem Typ, entsteht keine Freigabe - eine Freigabe ohne Ziel waere
                // schlimmer als keine, weil sie so viel gewaehrt wie die Pfadmuster hergeben.
                var declarations = ReadArguments(database, assetTmp.AssetTemplateId);
                if (!AssetArgumentValues.TryCreate(declarations, argumentValues, out var values, out error))
                {
                    LogEnvironment.LogEvent($"Die Freigabe wurde nicht angelegt: {error}", LogSeverity.Warning);
                    return null;
                }

                var currentTenant = database.Tenants.First(n => n.TenantId == database.CurrentTenantId);
                    var asset = new TSharedAsset
                    {
                        AssetKey = Guid.NewGuid().ToString("N"),
                        Template = assetTmp,
                        AssetTitle = title,
                        TenantId = database.CurrentTenantId.Value,
                        RootPath = requestPath,
                        AnonymousAccessTokenRaw = Guid.NewGuid().ToString("B"),
                        ArgumentValuesJson = values.IsEmpty ? null : values.ToJson(),
                        RecipientLabel = recipientLabel
                    };

                    database.SharedAssets.Add(asset);
                    database.SaveChanges();
                    return new FullAssetInfo()
                    {
                        AssetTitle = asset.AssetTitle,
                        UserScopeName = currentTenant.TenantName,
                        Features = assetTmp.FeatureGrants.Select(n => n.Feature.FeatureName).ToArray(),
                        Permissions = assetTmp.Grants.Select(n => n.Permission.PermissionName).ToArray(),
                        AssetKey = asset.AssetKey,
                        AssetRootPath = requestPath,
                        AnonymousAccessTokenRaw = asset.AnonymousAccessTokenRaw,
                        Arguments = declarations,
                        Values = values,
                        Enforcement = assetTmp.ArgumentEnforcement
                    };
            }

            error ??= "The template does not apply to this location, or you may not share here.";
            return null;
        }

        /// <summary>
        /// Die Argumente einer Vorlage, in ihrer Anzeigereihenfolge. Ueber die logische Referenz statt
        /// ueber eine Beziehung - die Vorlage ist generisch, die Argumenttabelle bewusst nicht.
        /// </summary>
        private static AssetArgumentDeclaration[] ReadArguments(TContext database, int assetTemplateId)
            => database.AssetTemplateArguments.Where(n => n.AssetTemplateId == assetTemplateId)
                .OrderBy(n => n.SortOrder)
                .Select(n => new AssetArgumentDeclaration(n.ArgumentName, n.ArgumentType, n.Required, n.ResolverKey))
                .ToArray();

        public bool UpdateSharedAsset(FullAssetInfo updatedInfo)
        {
            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            var asset = database.SharedAssets.FirstOrDefault(n => n.AssetKey == updatedInfo.AssetKey);
            if (asset == null)
            {
                // Zwei offene Masken auf derselben Uebersicht genuegen dafuer: die andere hat sie geloescht.
                LogEnvironment.LogEvent(
                    $"Die Freigabe '{updatedInfo.AssetKey}' existiert nicht mehr - nichts geaendert.",
                    LogSeverity.Warning);
                return false;
            }

            var ok = (asset.Template.RequiredFeature == null ||
                      services.VerifyActivatedFeatures(new[] { asset.Template.RequiredFeature.FeatureName }, out _)) &&
                     (asset.Template.RequiredPermission == null ||
                      services.VerifyUserPermissions(new[] { asset.Template.RequiredPermission.PermissionName }, out _));
            if (ok && database.CurrentTenantId != null && asset.TenantId == database.CurrentTenantId)
            {
                var auf = updatedInfo.UserShares.Union(from t in asset.UserFilters select t.LabelFilter)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var tusr = (from a in auf
                    join ori in asset.UserFilters on a.ToLower() equals ori.LabelFilter.ToLower() into oril
                    from lori in oril.DefaultIfEmpty()
                    join upd in updatedInfo.UserShares on a.ToLower() equals upd.ToLower() into updl
                    from lupd in updl.DefaultIfEmpty()
                    select new { L = a, O = lori, N = lupd != null }).ToArray();
                var asf = updatedInfo.UserScopeShares.Union(from t in asset.TenantFilters select t.LabelFilter)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var tscp = (from a in asf
                    join ori in asset.TenantFilters on a.ToLower() equals ori.LabelFilter.ToLower() into oril
                    from lori in oril.DefaultIfEmpty()
                    join upd in updatedInfo.UserScopeShares on a.ToLower() equals upd.ToLower() into updl
                    from lupd in updl.DefaultIfEmpty()
                    select new { L = a, O = lori, N = lupd != null }).ToArray();
                foreach (var u in tusr)
                {
                    if (u.O != null && !u.N)
                    {
                        database.SharedAssetUserFilters.Remove(u.O);
                    }
                    else if (u.O == null && u.N)
                    {
                        database.SharedAssetUserFilters.Add(new TSharedAssetUserFilter
                        {
                            LabelFilter = u.L,
                            Asset = asset
                        });
                    }
                }

                foreach (var s in tscp)
                {
                    if (s.O != null && !s.N)
                    {
                        database.SharedAssetTenantFilters.Remove(s.O);
                    }
                    else if (s.O == null && s.N)
                    {
                        database.SharedAssetTenantFilters.Add(new TSharedAssetTenantFilter
                        {
                            LabelFilter = s.L,
                            Asset = asset
                        });
                    }
                }

                asset.NotBefore = updatedInfo.NotBefore;
                asset.NotAfter = updatedInfo.NotAfter;
                asset.AssetTitle = updatedInfo.AssetTitle;
                asset.RecipientLabel = updatedInfo.RecipientLabel;
                database.SaveChanges();
                return true;
            }

            return false;
        }

        public bool DeleteSharedAsset(FullAssetInfo assetInfo)
        {
            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            var asset = database.SharedAssets.FirstOrDefault(n => n.AssetKey == assetInfo.AssetKey);
            if (asset == null)
            {
                // Zweimal auf Loeschen geklickt ist kein Server-Fehler; die Freigabe ist weg, das war das Ziel.
                LogEnvironment.LogEvent(
                    $"Die Freigabe '{assetInfo.AssetKey}' existiert nicht mehr - nichts zu loeschen.",
                    LogSeverity.Warning);
                return false;
            }

            var ok = (asset.Template.RequiredFeature == null ||
                      services.VerifyActivatedFeatures(new[] { asset.Template.RequiredFeature.FeatureName }, out _)) &&
                     (asset.Template.RequiredPermission == null ||
                      services.VerifyUserPermissions(new[] { asset.Template.RequiredPermission.PermissionName }, out _));
            if (ok && database.CurrentTenantId != null && asset.TenantId == database.CurrentTenantId)
            {
                database.SharedAssets.Remove(asset);
                database.SaveChanges();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Erzeugt den Link fuer einen anonymen Empfaenger: derselbe Pfad wie <see cref="CreateLink"/>, nur
        /// traegt der Abschnitt zusaetzlich das Zugangs-Token.
        /// </summary>
        public string CreateAnonymousLink(AssetInfo info, HttpContext context)
            => CreateAnonymousLink(info, Origin(context));

        public string CreateAnonymousLink(AssetInfo info, string origin)
        {
            var anonymousProvider = services.GetService<IAnonymousAssetLinkProvider>();
            if (anonymousProvider == null || info is not FullAssetInfo fin)
            {
                LogEnvironment.LogEvent(
                    "Kein IAnonymousAssetLinkProvider registriert oder keine vollen Asset-Angaben vorhanden - es kann kein anonymer Link erzeugt werden.",
                    LogSeverity.Error);
                return null;
            }

            return BuildLink(info, origin, anonymousProvider.CreateAnonymousToken(fin));
        }

        /// <summary>
        /// Erzeugt den Link fuer einen angemeldeten Empfaenger. Der Asset-Abschnitt steht ganz vorne, davor
        /// nur Schema und Host - siehe <see cref="SharedAssetPath"/>.
        /// </summary>
        public string CreateLink(AssetInfo info, HttpContext context)
            => CreateLink(info, Origin(context));

        public string CreateLink(AssetInfo info, string origin)
        {
            return BuildLink(info, origin, null);
        }

        /// <summary>
        /// Schema und Host der laufenden Anfrage. Im Blazor-Circuit gibt es keine - dort reicht der
        /// Aufrufer den Ursprung selbst herein.
        /// </summary>
        private static string Origin(HttpContext context)
            => context == null ? string.Empty : $"{context.Request.Scheme}://{context.Request.Host}";

        private string BuildLink(AssetInfo info, string origin, string accessToken)
        {
            var segment = SharedAssetPath.BuildSegment(info.AssetKey, accessToken);
            // Der Mandant gehoert nur in den Link, wenn ihn dieser Host ueberhaupt im Pfad fuehrt. Das ist
            // dieselbe Bedingung, unter der IUrlFormat den Scope einsetzt.
            var scope = services.GetService<IPermissionScope>();
            var tenant = scope is { IsScopeExplicit: true } ? scope.PermissionPrefix : null;
            var prefix = SharedAssetPath.BuildPrefix(segment, tenant);
            var rootPath = string.IsNullOrEmpty(info.AssetRootPath) ? "/" : info.AssetRootPath;
            if (!rootPath.StartsWith("/", StringComparison.Ordinal))
            {
                rootPath = "/" + rootPath;
            }

            return $"{origin}{prefix}{rootPath}";
        }

        /// <summary>
        /// Die Freigaben des aktuellen Mandanten. Der Mandantenfilter des Kontexts zieht die Grenze; die
        /// Vorlage entscheidet zusaetzlich, wer sie ueberhaupt sehen darf.
        /// </summary>
        public SharedAssetListItem[] ListSharedAssets(string search, int skip, int take, out int total)
        {
            total = 0;
            if (ImpersonationDeactivated)
            {
                return Array.Empty<SharedAssetListItem>();
            }

            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            if (database.CurrentTenantId == null)
            {
                return Array.Empty<SharedAssetListItem>();
            }

            var query = database.SharedAssets.Where(n => n.TenantId == database.CurrentTenantId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(n => n.AssetTitle.Contains(term) || n.RootPath.Contains(term));
            }

            // Erst materialisieren, dann filtern: ob jemand eine Vorlage benutzen darf, beantwortet der
            // Berechtigungsweg und keine Datenbankabfrage.
            var candidates = query.OrderByDescending(n => n.SharedAssetId).ToArray()
                .Where(n => (n.Template.RequiredFeature == null ||
                             services.VerifyActivatedFeatures(new[] { n.Template.RequiredFeature.FeatureName }, out _)) &&
                            (n.Template.RequiredPermission == null ||
                             services.VerifyUserPermissions(new[] { n.Template.RequiredPermission.PermissionName }, out _)))
                .ToArray();
            total = candidates.Length;
            return candidates.Skip(skip).Take(take).Select(n => new SharedAssetListItem
            {
                AssetKey = n.AssetKey,
                AssetTitle = n.AssetTitle,
                TemplateKey = n.Template.SystemKey,
                TemplateTitle = n.Template.Name,
                RootPath = n.RootPath,
                NotBefore = n.NotBefore,
                NotAfter = n.NotAfter,
                RecipientLabel = n.RecipientLabel,
                ArgumentSummary = Summarize(n.ArgumentValuesJson),
                IsAnonymous = n.UserFilters.Any(f => f.LabelFilter == AnonymousTag),
                IsPublic = n.UserFilters.Any(f => f.LabelFilter == "%")
                           || n.TenantFilters.Any(f => f.LabelFilter == "%")
            }).ToArray();
        }

        /// <summary>
        /// Erneuert das Geheimnis: verschickte anonyme Links werden ungueltig, die Freigabe bleibt.
        /// </summary>
        public bool RotateAnonymousToken(string assetKey)
        {
            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            var asset = database.SharedAssets.FirstOrDefault(n => n.AssetKey == assetKey);
            if (asset == null)
            {
                LogEnvironment.LogEvent($"Keine Freigabe mit dem Schluessel '{assetKey}' gefunden - das Geheimnis wurde nicht erneuert.", LogSeverity.Warning);
                return false;
            }

            var ok = (asset.Template.RequiredFeature == null ||
                      services.VerifyActivatedFeatures(new[] { asset.Template.RequiredFeature.FeatureName }, out _)) &&
                     (asset.Template.RequiredPermission == null ||
                      services.VerifyUserPermissions(new[] { asset.Template.RequiredPermission.PermissionName }, out _));
            if (!ok || database.CurrentTenantId == null || asset.TenantId != database.CurrentTenantId)
            {
                LogEnvironment.LogEvent($"Das Geheimnis der Freigabe '{assetKey}' durfte nicht erneuert werden.", LogSeverity.Warning);
                return false;
            }

            asset.AnonymousAccessTokenRaw = Guid.NewGuid().ToString("B");
            database.SaveChanges();
            return true;
        }

        /// <summary>
        /// Erzeugt ein Ad-hoc-Ticket. Es wird NICHT gespeichert - die Nutzlast reist verschluesselt in
        /// der URL, und die Rechte kommen aus der Vorlage, auf die sie zeigt.
        /// </summary>
        public string CreateAdHocTicket(string requestPath, AssetTemplateInfo template,
            IDictionary<string, string> argumentValues, string recipientLabel, TimeSpan? lifetime, string origin,
            out string error)
        {
            error = null;
            requestPath = Canonical(requestPath);
            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            var assetTmp = database.AssetTemplates.FirstOrDefault(n => n.SystemKey == template.TemplateKey);
            if (assetTmp == null)
            {
                error = "The template does not exist.";
                return null;
            }

            if (!assetTmp.AllowAdHoc)
            {
                // Bewusst eine eigene Meldung: "geht nicht" waere im Support wertlos, und dieser Fall ist
                // eine Einstellung an der Vorlage und kein Fehler des Benutzers.
                error = "This template does not allow ad-hoc tickets.";
                return null;
            }

            var ok = (assetTmp.RequiredFeature == null ||
                      services.VerifyActivatedFeatures(new[] { assetTmp.RequiredFeature.FeatureName }, out _)) &&
                     (assetTmp.RequiredPermission == null ||
                      services.VerifyUserPermissions(new[] { assetTmp.RequiredPermission.PermissionName }, out _));
            if (!ok || database.CurrentTenantId == null || !IsTemplateValidForPath(assetTmp, requestPath))
            {
                error = "The template does not apply to this location, or you may not share here.";
                return null;
            }

            var declarations = ReadArguments(database, assetTmp.AssetTemplateId);
            if (!AssetArgumentValues.TryCreate(declarations, argumentValues, out var values, out error))
            {
                return null;
            }

            // Die Frist ist Pflicht und nach oben begrenzt: ein Ticket laesst sich nicht einzeln
            // loeschen, also muss es von selbst enden.
            var maximum = TimeSpan.FromMinutes(Math.Max(1, assetTmp.MaxAdHocMinutes));
            var effective = lifetime == null || lifetime.Value > maximum || lifetime.Value <= TimeSpan.Zero
                ? maximum
                : lifetime.Value;
            var currentTenant = database.Tenants.First(n => n.TenantId == database.CurrentTenantId);
            var ticket = new AssetTicket
            {
                TemplateKey = assetTmp.SystemKey,
                RootPath = requestPath,
                ArgumentValues = values.Names.ToDictionary(n => n, n => values[n]),
                NotBefore = null,
                NotAfter = DateTime.UtcNow.Add(effective),
                Nonce = Guid.NewGuid().ToString("N"),
                RecipientLabel = recipientLabel
            };

            var raw = securityRepo.Encrypt(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ticket)),
                currentTenant.TenantName);
            var segment = SharedAssetPath.BuildTicketSegment(currentTenant.TenantName,
                WebEncoders.Base64UrlEncode(raw));
            var prefix = SharedAssetPath.BuildPrefix(segment, TenantInPath());
            var rootPath = string.IsNullOrEmpty(requestPath) ? "/" : requestPath;
            if (!rootPath.StartsWith("/", StringComparison.Ordinal))
            {
                rootPath = "/" + rootPath;
            }

            return $"{origin}{prefix}{rootPath}";
        }

        /// <summary>
        /// Loest ein Ad-hoc-Ticket auf. Jede Pruefung, die fehlschlaegt, ergibt null - und einen Eintrag
        /// im Log, denn der Empfaenger sieht sonst nur eine Seite ohne Inhalt.
        /// </summary>
        public AssetInfo GetTicketInfo(string tenantName, string payload, ClaimsPrincipal requestor)
        {
            if (ImpersonationDeactivated || string.IsNullOrEmpty(tenantName) || string.IsNullOrEmpty(payload))
            {
                return null;
            }

            AssetTicket ticket;
            try
            {
                var raw = securityRepo.Decrypt(WebEncoders.Base64UrlDecode(payload), tenantName);
                ticket = JsonSerializer.Deserialize<AssetTicket>(Encoding.UTF8.GetString(raw));
            }
            catch (Exception ex)
            {
                // Veraendert, mit einem fremden Schluessel erzeugt oder schlicht kaputt - in jedem Fall
                // kein gueltiges Ticket. Die Unterscheidung waere fuer den Aufrufer wertlos und fuer
                // einen Angreifer eine Auskunft.
                LogEnvironment.LogEvent($"Ein Ad-hoc-Ticket liess sich nicht lesen: {ex.OutlineException()}",
                    LogSeverity.Warning);
                return null;
            }

            if (ticket == null || string.IsNullOrEmpty(ticket.TemplateKey))
            {
                LogEnvironment.LogEvent("Ein Ad-hoc-Ticket war leer oder nannte keine Vorlage.", LogSeverity.Warning);
                return null;
            }

            var now = DateTime.UtcNow;
            if (ticket.NotBefore != null && now < ticket.NotBefore.Value)
            {
                LogEnvironment.LogEvent($"Das Ad-hoc-Ticket '{ticket.Nonce}' gilt noch nicht.", LogSeverity.Warning);
                return null;
            }

            if (now > ticket.NotAfter)
            {
                LogEnvironment.LogEvent($"Das Ad-hoc-Ticket '{ticket.Nonce}' ist abgelaufen.", LogSeverity.Warning);
                return null;
            }

            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            using var h = securityAccessProvider.CreateForCaller(database,
                ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
            if (database.RevokedAssetTickets.Any(n => n.Nonce == ticket.Nonce))
            {
                LogEnvironment.LogEvent($"Das Ad-hoc-Ticket '{ticket.Nonce}' wurde zurueckgezogen.", LogSeverity.Warning);
                return null;
            }

            var assetTmp = database.AssetTemplates.FirstOrDefault(n => n.SystemKey == ticket.TemplateKey);
            if (assetTmp == null || !assetTmp.AllowAdHoc)
            {
                // Der Grob-Widerruf: wer die Vorlage abschaltet, entwertet alle Tickets, die auf sie
                // zeigen. Deshalb steht die Vorlage im Ticket und nicht ihre Rechte.
                LogEnvironment.LogEvent(
                    $"Die Vorlage '{ticket.TemplateKey}' des Ad-hoc-Tickets fehlt oder erlaubt keine Tickets mehr.",
                    LogSeverity.Warning);
                return null;
            }

            if (!IsTemplateValidForPath(assetTmp, Canonical(services.GetService<IContextUserProvider>()?.RequestPath)))
            {
                LogEnvironment.LogEvent(
                    $"Das Ad-hoc-Ticket '{ticket.Nonce}' gilt an dieser Stelle nicht.", LogSeverity.Warning);
                return null;
            }

            var declarations = ReadArguments(database, assetTmp.AssetTemplateId);
            var values = AssetArgumentValues.FromJson(JsonSerializer.Serialize(ticket.ArgumentValues));
            if (!IsStillValid(assetTmp.ValidityRuleKey, values, ticket.Nonce))
            {
                return null;
            }

            return new AssetInfo
            {
                AssetKey = ticket.Nonce,
                AssetTitle = null,
                AssetRootPath = ticket.RootPath,
                UserScopeName = tenantName,
                Features = assetTmp.FeatureGrants.Select(n => n.Feature.FeatureName).ToArray(),
                Permissions = assetTmp.Grants.Select(n => n.Permission.PermissionName).ToArray(),
                Arguments = declarations,
                Values = values,
                Enforcement = assetTmp.ArgumentEnforcement,
                AuditMode = assetTmp.AuditMode,
                TemplateSystemKey = assetTmp.SystemKey,
                TicketNonce = ticket.Nonce,
                RecipientLabel = ticket.RecipientLabel
            };
        }

        /// <summary>
        /// Zieht ein Ad-hoc-Ticket zurueck.
        /// </summary>
        public bool RevokeTicket(string nonce, DateTime expiresUtc)
        {
            if (string.IsNullOrEmpty(nonce))
            {
                return false;
            }

            using var lease = contextFactory.Lease<TContext>();
            var database = lease.Context;
            using var h = securityAccessProvider.CreateForCaller(database,
                ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
            if (database.RevokedAssetTickets.Any(n => n.Nonce == nonce))
            {
                return true;
            }

            database.RevokedAssetTickets.Add(new RevokedAssetTicket
            {
                Nonce = nonce,
                ExpiresUtc = expiresUtc,
                RevokedUtc = DateTime.UtcNow
            });
            database.SaveChanges();
            return true;
        }

        /// <summary>
        /// Fragt die Gueltigkeitsregel der Vorlage - "gilt das noch?" ist manchmal keine Frage des
        /// Datums. Ohne benannte Regel gilt es.
        /// </summary>
        private bool IsStillValid(string validityRuleKey, AssetArgumentValues values, string reference)
        {
            if (string.IsNullOrEmpty(validityRuleKey))
            {
                return true;
            }

            var rules = services.GetService<IEnumerable<IAssetValidityRule>>();
            var rule = rules?.FirstOrDefault(n =>
                string.Equals(n.Key, validityRuleKey, StringComparison.OrdinalIgnoreCase));
            if (rule == null)
            {
                // Eine benannte, aber nicht registrierte Regel ist ein Verdrahtungsfehler. Sie zu
                // ignorieren hiesse, eine Freigabe laenger gelten zu lassen, als jemand gemeint hat.
                LogEnvironment.LogEvent(
                    $"Die Gueltigkeitsregel '{validityRuleKey}' ist nicht registriert - der Zugriff auf '{reference}' wird abgelehnt.",
                    LogSeverity.Error);
                return false;
            }

            if (rule.IsValid(values))
            {
                return true;
            }

            LogEnvironment.LogEvent($"Die Gueltigkeitsregel '{validityRuleKey}' beendet den Zugriff auf '{reference}'.",
                LogSeverity.Warning);
            return false;
        }

        /// <summary>
        /// Der Mandant, soweit ihn dieser Host im Pfad fuehrt - dieselbe Bedingung wie beim Linkbau einer
        /// gespeicherten Freigabe.
        /// </summary>
        private string TenantInPath()
        {
            var scope = services.GetService<IPermissionScope>();
            return scope is { IsScopeExplicit: true } ? scope.PermissionPrefix : null;
        }

        /// <summary>
        /// Fasst die Argumentwerte lesbar zusammen - was in der Uebersicht die Frage beantwortet, worauf
        /// eine Freigabe eigentlich zeigt.
        /// </summary>
        private static string Summarize(string argumentValuesJson)
        {
            var values = AssetArgumentValues.FromJson(argumentValuesJson);
            return values.IsEmpty
                ? string.Empty
                : string.Join(", ", values.Names.Select(n => $"{n}={values[n]}"));
        }

        public FullAssetInfo FindAnonymousAsset(string assetKey)
        {
            if (!ImpersonationDeactivated)
            {
                using var lease = contextFactory.Lease<TContext>();
                var database = lease.Context;
                using var h = securityAccessProvider.CreateForCaller(database, ConfigureTrustConfig(new() {ShowAllTenants = true, HideGlobals = false}));
                var rawAsset = (from t in database.SharedAssets
                    join a in database.SharedAssetUserFilters on t.SharedAssetId equals a.SharedAssetId
                    where a.LabelFilter == AnonymousTag && t.AssetKey == assetKey
                    select t).FirstOrDefault();
                if (rawAsset != null)
                {
                    var retVal = new FullAssetInfo
                    {
                        AnonymousAccessTokenRaw = rawAsset.AnonymousAccessTokenRaw,
                        AssetKey = rawAsset.AssetKey,
                        AssetRootPath = rawAsset.RootPath,
                        AssetTitle = rawAsset.AssetTitle,
                        NotAfter = rawAsset.NotAfter,
                        NotBefore = rawAsset.NotBefore,
                        UserScopeName = rawAsset.AssetOwner.TenantName,
                        Permissions = rawAsset.Template.Grants.Select(n => n.Permission.PermissionName).ToArray(),
                        Features = rawAsset.Template.FeatureGrants.Select(n => n.Feature.FeatureName).ToArray(),
                        Arguments = ReadArguments(database, rawAsset.AssetTemplateId),
                        Values = AssetArgumentValues.FromJson(rawAsset.ArgumentValuesJson),
                        Enforcement = rawAsset.Template.ArgumentEnforcement,
                        RecipientLabel = rawAsset.RecipientLabel
                    };
                    retVal.UserShares.AddRange(rawAsset.UserFilters.Select(n => n.LabelFilter));
                    retVal.UserScopeShares.AddRange(rawAsset.TenantFilters.Select(n => n.LabelFilter));
                    return retVal;
                }
            }

            return null;
        }

        void ISharedAssetAdapter.SetImpersonationOff()
        {
            lock (sync)
            {
                impersonationDeactivated++;
            }
        }

        void ISharedAssetAdapter.SetImpersonationOn()
        {
            lock (sync)
            {
                impersonationDeactivated--;
            }
        }

        protected abstract TTrustConfig ConfigureTrustConfig(TTrustConfig trustConfig);

        private bool AssetIsAccessible(TContext database, string assetKey, IdentityInfo[] userLabels, string[] tenants, out TSharedAsset asset)
        {
            asset = database.SharedAssets.FirstOrDefault(n => n.AssetKey == assetKey);
            if (asset == null)
            {
                // Ein Schluessel, zu dem es keine Freigabe (mehr) gibt: geloescht, aus einer anderen
                // Umgebung, oder von Hand zusammengebaut. Das ist die haeufigste Art, wie ein alter Link
                // wiederkommt - und es ist kein Fehler des Servers, sondern schlicht kein Zugriff. Er kam
                // hier aber als unbehandelte Ausnahme heraus, weil ueber den Schluessel eines BESUCHERS
                // abgefragt wird und `First` verlangt, was der Besucher nicht garantieren kann.
                LogEnvironment.LogEvent(
                    $"Keine Freigabe mit dem Schluessel '{assetKey}' gefunden - kein Zugriff.",
                    LogSeverity.Warning);
                return false;
            }

            var uf = asset.UserFilters.Select(n => n.LabelFilter).ToArray();
            var tf = asset.TenantFilters.Select(n => n.LabelFilter).ToArray();
            DateTime now = DateTime.UtcNow;
            bool legit = false;
            foreach (var l in userLabels)
            {
                if (uf.Any(n => l.Labels.Any(ul => n.Equals(ul, StringComparison.OrdinalIgnoreCase)) || n == AnonymousTag || n == "%"))
                {
                    legit = true;
                    break;
                }
            }

            if (!legit)
            {
                foreach (var t in tenants)
                {
                    if (tf.Any(n => n.Equals(t, StringComparison.OrdinalIgnoreCase) || n == "%"))
                    {
                        legit = true;
                        break;
                    }
                }
            }

            if (legit && (asset.NotBefore != null || asset.NotAfter != null))
            {
                var nb = asset.NotBefore ?? DateTime.MinValue;
                var na = asset.NotAfter ?? DateTime.MaxValue;
                legit &= now.Date >= nb && now.Date <= na;
            }

            return legit;
        }

        /// <summary>
        /// Bringt einen Anfragepfad auf die Form, in der <c>RootPath</c> und die Pfadmuster der Vorlage
        /// gespeichert sind: ohne Asset-Abschnitt und ohne Mandantensegment - so, wie ihn die Route sieht.
        /// <para>
        /// Ohne das wuerde derselbe Vergleich je nach Host gegen verschiedene Pfade laufen: in MVC steht der
        /// Mandant noch im Pfad, in Blazor liegt er bereits in <c>PathBase</c>. Gespeichert werden kann aber
        /// nur eine der beiden Formen.
        /// </para>
        /// </summary>
        /// <param name="requestPath">der Pfad, wie ihn der Aufrufer kennt</param>
        /// <returns>der praefixfreie Pfad</returns>
        private string Canonical(string requestPath)
        {
            var assetSegment = services.GetService<ISharedAssetContext>()?.Segment;
            var tenant = services.GetService<IPermissionScope>()?.PermissionPrefix;
            return SharedAssetPath.Canonicalize(requestPath, assetSegment, tenant);
        }

        private bool IsTemplateValidForPath(TAssetTemplate template, string requestPath)
        {
            var urls = template.PathTemplates.Select(n => n.PathTemplate).ToArray();
            var retVal = urls.Any(n => Regex.IsMatch(requestPath, n,
                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline));
            return retVal;
        }
    }
}
