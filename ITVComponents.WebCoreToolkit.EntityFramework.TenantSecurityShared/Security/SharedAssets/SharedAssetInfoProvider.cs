using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Security.SharedAssets
{
    public abstract class SharedAssetInfoProvider<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TContext> : ISharedAssetAdapter
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TNavigationMenu : NavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TTenantNavigation : TenantNavigationMenu<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation>
        where TQuery : DiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TTenantQuery : TenantDiagnosticsQuery<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TQueryParameter : DiagnosticsQueryParameter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery>
        where TWidget : DashboardWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetParam : DashboardParam<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TWidgetLocalization : DashboardWidgetLocalization<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserWidget : UserWidget<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization>
        where TUserProperty : CustomUserProperty<TUserId, TUser>
        where TUser : class
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>, new()
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TContext: DbContext, ISecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation>
        where TTenant : Tenant
        where TWebPlugin:WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant:WebPluginConstant<TTenant>
        where TWebPluginGenericParameter:WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence:Sequence<TTenant>
        where TTenantSetting:TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
    {
        public const string AnonymousTag = "##ANONYMOUS";
        private readonly IUserNameMapper userNameMapper;
        private readonly ISecurityRepository securityRepo;
        private readonly TContext database;
        private readonly IServiceProvider services;
        private object sync = new();
        private int impersonationDeactivated = 0;

        public SharedAssetInfoProvider(IUserNameMapper userNameMapper, ISecurityRepository securityRepo, TContext database, IServiceProvider services)
        {
            this.userNameMapper = userNameMapper;
            this.securityRepo = securityRepo;
            this.database = database;
            this.services = services;
        }

        protected bool ImpersonationDeactivated => impersonationDeactivated != 0;

        public AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false)
        {
            if (!ImpersonationDeactivated)
            {
                var labels = requestor.Identities.Where(n => n.IsAuthenticated).Select(t => new IdentityInfo
                    { Labels = userNameMapper.GetUserLabels(t), AuthenticationType = t.AuthenticationType }).ToArray();
                var tenants = labels.SelectMany(i =>
                        securityRepo.GetEligibleScopes(i.Labels, i.AuthenticationType).Select(n => n.ScopeName))
                    .Distinct()
                    .ToArray();
                bool accessible = AssetIsAccessible(assetKey, labels, tenants, out var asset);
                AssetInfo retVal;
                bool hasOwnerPrivileges = false;
                if (asOwner)
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
                    return retVal;
                }
            }

            return null;
        }

        public bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor)
        {
            if (!ImpersonationDeactivated)
            {
                var labels = requestor.Identities.Where(n => n.IsAuthenticated).Select(t => new IdentityInfo
                    { Labels = userNameMapper.GetUserLabels(t), AuthenticationType = t.AuthenticationType }).ToArray();
                var tenants = labels.SelectMany(i =>
                        securityRepo.GetEligibleScopes(i.Labels, i.AuthenticationType).Select(n => n.ScopeName))
                    .Distinct()
                    .ToArray();
                bool retVal = AssetIsAccessible(assetKey, labels, tenants, out var asset);
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
            if (!ImpersonationDeactivated)
            {
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
                            TemplateKey = template.SystemKey
                        });
                    }
                }

                return retVal.ToArray();
            }

            return Array.Empty<AssetTemplateInfo>();
        }

        public AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title)
        {
            var assetTmp = database.AssetTemplates.First(n => n.SystemKey == template.TemplateKey);
            var ok = (assetTmp.RequiredFeature == null ||
                      services.VerifyActivatedFeatures(new[] { assetTmp.RequiredFeature.FeatureName }, out _)) &&
                     (assetTmp.RequiredPermission == null ||
                      services.VerifyUserPermissions(new[] { assetTmp.RequiredPermission.PermissionName }, out _));
            if (ok && database.CurrentTenantId != null && IsTemplateValidForPath(assetTmp, requestPath))
            {

                var currentTenant = database.Tenants.First(n => n.TenantId == database.CurrentTenantId);
                    var asset = new TSharedAsset
                    {
                        AssetKey = Guid.NewGuid().ToString("N"),
                        Template = assetTmp,
                        AssetTitle = title,
                        TenantId = database.CurrentTenantId.Value,
                        RootPath = requestPath,
                        AnonymousAccessTokenRaw = Guid.NewGuid().ToString("B")
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
                        AnonymousAccessTokenRaw = asset.AnonymousAccessTokenRaw
                    };
            }

            return null;
        }

        public bool UpdateSharedAsset(FullAssetInfo updatedInfo)
        {
            var asset = database.SharedAssets.First(n => n.AssetKey == updatedInfo.AssetKey);
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
                database.SaveChanges();
                return true;
            }

            return false;
        }

        public bool DeleteSharedAsset(FullAssetInfo assetInfo)
        {
            var asset = database.SharedAssets.First(n => n.AssetKey == assetInfo.AssetKey);
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

        public string CreateAnonymousLink(AssetInfo info, HttpContext context)
        {
            var baseUrl = CreateLink(info, context);
            var anonymousProvider = services.GetService<IAnonymousAssetLinkProvider>();
            if (anonymousProvider != null && info is FullAssetInfo fin)
            {
                baseUrl = anonymousProvider.CreateAnonymousLink(baseUrl, fin);
                return baseUrl;
            }

            return null;
        }

        public string CreateLink(AssetInfo info, HttpContext context)
        {
            var quid = info.AssetRootPath.Contains('?');
            var nop = !info.AssetRootPath.EndsWith('?');
            return $"{context.Request.Scheme}://{context.Request.Host}{info.AssetRootPath}{(!nop?(quid?"&":"?"):string.Empty)}{WebCoreToolkit.Global.FixedAssetRequestQueryParameter}={info.AssetKey}";
        }

        public FullAssetInfo FindAnonymousAsset(string assetKey)
        {
            if (!ImpersonationDeactivated)
            {
                using var h = FullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>.CreateForCaller(database, database, new(){ShowAllTenants = true, HideGlobals = false});
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
                        Features = rawAsset.Template.FeatureGrants.Select(n => n.Feature.FeatureName).ToArray()
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

        private bool AssetIsAccessible(string assetKey, IdentityInfo[] userLabels, string[] tenants, out TSharedAsset asset)
        {
            asset = database.SharedAssets.First(n => n.AssetKey == assetKey);
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

        private bool IsTemplateValidForPath(TAssetTemplate template, string requestPath)
        {
            var urls = template.PathTemplates.Select(n => n.PathTemplate).ToArray();
            var retVal = urls.Any(n => Regex.IsMatch(requestPath, n,
                RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline));
            return retVal;
        }
    }
}
