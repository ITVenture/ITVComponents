using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using Feature = ITVComponents.WebCoreToolkit.Models.Feature;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.Helpers;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.TypeConversion;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.Formatting;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using ITVComponents.Json;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Security
{
    public abstract class DbSecurityRepository<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> : ISecurityRepository
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
        where TUserProperty : CustomUserProperty<TUserId, TUser>, new()
        where TAssetTemplate : AssetTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplatePath : AssetTemplatePath<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateGrant : AssetTemplateGrant<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TAssetTemplateFeature : AssetTemplateFeature<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature>
        where TSharedAsset : SharedAsset<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetUserFilter : SharedAssetUserFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TSharedAssetTenantFilter : SharedAssetTenantFilter<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet>
        where TClientAppTemplatePermission : ClientAppTemplatePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppTemplate, TClientAppTemplatePermission>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TClientAppUser : ClientAppUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppUser>
        where TUser : class
        where TTenant : HierarchyTenant
        where TWebPlugin : WebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : WebPluginConstant<TTenant>
        where TWebPluginGenericParameter : WebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : TenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole>
        where TTrustConfig : HierarchyTenantContextSecurityTrustConfig, new()
    {
        private readonly IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> securityContext;
        private readonly ILogger logger;

        protected DbSecurityRepository(IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TTrustConfig> securityContext,
            ILogger logger)
        {
            this.securityContext = securityContext;
            this.logger = logger;
        }

        public string UniqueName { get; set; }

        public ICollection<User> Users
        {
            get
            {
                return (from u in securityContext.Users.ToList()
                    select SelectUser(u)).ToList();
            }
        }

        public ICollection<Role> Roles
        {
            get
            {
                using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                    securityContext,
                    new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
                return (from r in securityContext.SecurityRoles where r.TenantId == securityContext.CurrentTenantId select r).ToList<Role>();
            }
        }

        public ICollection<Permission> Permissions
        {
            get
            {
                using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                    securityContext,
                    new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
                return (from p in securityContext.Permissions where p.TenantId == null || p.TenantId == securityContext.CurrentTenantId select p).ToList<Permission>();
            }
        }

        public IEnumerable<Role> GetRoles(User user)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                securityContext,
                new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = true});
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                securityContext,
                new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
            return (from a in (from t in securityContext.SecurityRoles.Where(r =>
                            r.Tenant.TenantName == permissionScope)
                        select new
                        {
                            PermissionMap = t.RolePermissions.Select(n =>
                                new { t.RoleName, Permission = n.Permission.PermissionName })
                        })
                    .SelectMany(i => i.PermissionMap).AsEnumerable()
                join p in requiredPermissions on a.Permission equals p
                select a.RoleName).Distinct().Select(n => new Role { RoleName = n }).ToArray();
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                securityContext,
                new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
            return (from p in UserProps(securityContext.Users.First(UserFilter(user))) where p.PropertyType == propertyType select p).ToArray();
        }

        public string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType)
        {
            string retVal = null;
            var tmp = GetCustomProperties(user, propertyType).FirstOrDefault(n => n.PropertyName == propertyName);
            if (tmp != null)
            {
                retVal = tmp.Value;
            }

            return retVal;
        }

        /// <summary>
        /// Gets the string representation of the given property. This is only supported in 1:1 user environments
        /// </summary>
        /// <param name="user">the user for which go get the property</param>
        /// <param name="propertyName">the name of the desired property</param>
        /// <param name="propertyType">the expected property-type</param>
        /// <returns>the string representation of the requested property</returns>
        public T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType)
        {
            T retVal = default(T);
            var tmpVal = GetCustomProperty(user, propertyName, propertyType);
            if (!string.IsNullOrEmpty(tmpVal))
            {
                if (propertyType == CustomUserPropertyType.Claim || propertyType == CustomUserPropertyType.Literal)
                {
                    if (TypeConverter.TryConvert(tmpVal, typeof(T), out var result))
                    {
                        retVal = (T)result;
                    }
                }
                else
                {
                    retVal = JsonHelper.FromJsonString<T>(tmpVal);
                }
            }

            return retVal;
        }

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                securityContext,
                new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
            var dbuser = securityContext.Users.First(UserFilter(user));
            var prop = securityContext.UserProperties.FirstOrDefault(n =>
                n.PropertyName == propertyName && n.User == dbuser && n.PropertyType == propertyType);
            if (prop == null && !string.IsNullOrEmpty(value))
            {
                prop = new TUserProperty
                {
                    PropertyType = propertyType,
                    PropertyName = propertyName,
                    User = dbuser
                };

                securityContext.UserProperties.Add(prop);
            }
            else if (prop != null && string.IsNullOrEmpty(value))
            {
                securityContext.UserProperties.Remove(prop);
                prop = null;
            }

            if (prop != null)
            {
                prop.Value = value;
            }

            securityContext.SaveChanges();
            return true;
        }

        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value)
        {
            string stringVal = null;
            if (propertyType == CustomUserPropertyType.Claim || propertyType == CustomUserPropertyType.Literal)
            {
                stringVal = value?.ToString();
            }
            else if (value != null)
            {
                stringVal = JsonHelper.ToJson(value);
            }

            return SetCustomProperty(user, propertyName, propertyType, stringVal);
        }

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            var t = securityContext.CurrentTenantId;
            if (t != null)
            {
                var ti = t.Value;
                IQueryable<UserTenantLevel<TUser>> tenantUsers;
                var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
                using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext,
                    new TTrustConfig { HideGlobals = false, IncludeParentTree = isUser, ShowAllTenants = false });
                if (isUser)
                {
                    //tenantUsers = securityContext.TenantUsers.Where(tu => tu.TenantId == ti).Select(u => u.User);
                    tenantUsers = GetRawUserQuery();
                }
                else
                {
                    var filteredLabels = (from ul in userLabels
                        where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                        select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                    var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ti);
                    tenantUsers = appUsers
                        .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                        .Select(n => new UserTenantLevel<TUser>{User=n.TenantUser.User,TenantId = ti, Level=1});
                }

                return tenantUsers.Select(n => n.User).Any(UserFilter(userLabels, userAuthenticationType));
            }

            return false;
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType,
            CustomUserPropertyType propertyType)
        {
            var isUser = userLabels.All(n => string.IsNullOrEmpty(n) || !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext,
                securityContext,
                new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser});
            IQueryable<UserTenantLevel<TUser>> tenantUsers;
            if (isUser)
            {
                tenantUsers= GetRawUserQuery();
                /*tenantUsers =  phase2.AsEnumerable()
                    .Select(t => t.Users.First(n => n.ParentLevel == t.Level).User).AsQueryable();*/
            }
            else
            {
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers;
                tenantUsers = appUsers
                    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                    .Select(n => new UserTenantLevel<TUser> { User = n.TenantUser.User, TenantId = n.TenantUser.TenantId, Level = 1 });
            }
            return (from u in tenantUsers.Select(n => n.User).Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(securityContext.UserProperties, UserId, p => p.UserId, (tu, tp) => tp)
                where u.PropertyType == propertyType
                select u).ToArray();
        }

        public IEnumerable<T> GetUserIds<T>(string[] userLabels, string userAuthenticationType)
        {
            if (typeof(T) != typeof(TUserId))
            {
                throw new InvalidOperationException($"Expected Type was: {typeof(T)}");
            }

            var isUser = userLabels.All(n => string.IsNullOrEmpty(n) || !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser}));
            IQueryable<UserTenantLevel<TUser>> tenantUsers;
            if (isUser)
            {
                tenantUsers = GetRawUserQuery();
            }
            else
            {
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = securityContext.ClientAppUsers;
                tenantUsers = appUsers
                    .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                    .Select(n => new UserTenantLevel<TUser> { User = n.TenantUser.User, TenantId = n.TenantUser.TenantId, Level = 1 });
            }

            return tenantUsers.Select(n => n.User).Where(UserFilter(userLabels, userAuthenticationType)).Select(UserId).Cast<T>().ToList();
        }

        public T GetUserId<T>(string[] userLabels, string userAuthenticationType)
        {
            var tmp = GetUserIds<T>(userLabels, userAuthenticationType).ToArray();
            if (tmp.Length != 1)
            {
                throw new InvalidOperationException("Use GetUserIds in Environment with User-Mappings!");
            }

            return tmp[0];
        }

        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false }));
            var typeClaims = securityContext.AuthenticationClaimMappings.Where(n =>
                n.AuthenticationType.AuthenticationTypeName == userAuthenticationType).ToArray();
            var claimMapRaw = new Dictionary<string, ClaimData[]>(from t in originalClaims group t by t.Type into g select new KeyValuePair<string, ClaimData[]>(g.Key, g.ToArray()));
            var claimMap = new ClaimMap(claimMapRaw);
            var preMapped = from t in originalClaims
                join i in typeClaims on t.Type equals i.IncomingClaimName
                select new
                {
                    Original = new ClaimMapRoot
                    {
                        Value = t.Value,
                        Issuer = t.Issuer,
                        OriginalIssuer = t.OriginalIssuer,
                        Type = t.Type,
                        ValueType = t.ValueType,
                        ClaimMap = claimMap
                    },
                    Map = i
                };
            return (from t in preMapped
                where string.IsNullOrEmpty(t.Map.Condition) || (ExpressionParser.Parse(t.Map.Condition, t.Original) is bool b && b)
                select TryGetClaim(t.Map, t.Original)).Where(n => n != null);
        }

        public IEnumerable<Permission> GetPermissions(User user)
        {
            return (from p in (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role.RolePermissions).SelectMany(rp => rp)
                select new Permission
                {
                    //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                    PermissionName = p.Permission.PermissionName
                }).Distinct().ToArray();
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext,
                ConfigureTrustConfig(new()
                    { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser }));

            if (isUser)
            {
                var preFiltered = GetRawUserQuery();
                /*(from t in securityContext.TenantUsers
                        join u in securityContext.UpwardsRoleUserPermissionsView on t.TenantUserId equals u
                            .TenantUserId
                        select new { t.TenantId, t.TenantUserId, t.UserId, u.ParentLevel, u.PermissionName }
                        into gpr
                        group gpr by new { gpr.UserId, gpr.ParentLevel }
                        into gpp
                        select new
                        {
                            UserId = gpp.Key.UserId,
                            Level = gpp.Key.ParentLevel,
                            Perms = gpp.Select(g => new
                                { g.UserId, g.TenantUserId, g.PermissionName, g.ParentLevel }).ToArray()
                        })
                    .AsEnumerable();*/

                var tmptu = securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType)).Join(
                    preFiltered,
                    UserId, IdOfUserLevelRecord, (l, r) => new { r.Level, r.TenantId, r.RoleId, User=l });
                var pr = (from t in tmptu
                    join r in securityContext.RolePermissions on t.RoleId equals r.RoleId
                    join p in securityContext.Permissions on r.PermissionId equals p.PermissionId
                    select new { t.TenantId, t.User, t.Level, Permission=p })
                    .Where(n => n.TenantId == securityContext.CurrentTenantId)
                    .Select(n => n.Permission).Distinct();
                return pr;
                //tenantUsers = securityContext.TenantUsers.Where(tu => tu.TenantId == securityContext.CurrentTenantId.Value).Select(u => u.User);
            }

            var filteredLabels = (from ul in userLabels
                where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
            var appUsers =
                securityContext.ClientAppUsers.Where(
                    n => n.TenantUser.TenantId == securityContext.CurrentTenantId.Value);
            var preFilteredPerms = appUsers.SelectMany(n => n.ClientApp.AppPermissions)
                .SelectMany(n => n.PermissionSet.Permissions)
                .Select(n => n.Permission.PermissionName).Distinct().ToArray();
            var tenantUsers = appUsers
                .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                .Select(n => n.TenantUser.User);
            var permRaw = (from tr in tenantUsers.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(securityContext.TenantUsers, UserId, tr => tr.UserId, (tu, tt) => tt)
                join ur in securityContext.TenantUserRoles /*.Where(n => n.TenantUserId != null && n.RoleId != null)*/
                    on tr.TenantUserId equals ur.TenantUserId.Value
                join r in securityContext.SecurityRoles on new { RoleId = ur.RoleId.Value, tr.TenantId } equals new
                    { r.RoleId, r.TenantId }
                join rp in securityContext.RolePermissions /*.Where(n => n.RoleId != null)*/
                    on new { r.RoleId, r.TenantId } equals new { RoleId = rp.RoleId, rp.TenantId }
                join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                select new Permission
                {
                    //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                    PermissionName = p.PermissionName
                }).Distinct().ToArray();
            if (preFilteredPerms != null)
            {
                permRaw = (from t in permRaw join p in preFilteredPerms on t.PermissionName equals p select t)
                    .ToArray();
            }

            return permRaw;
        }

        public IEnumerable<Permission> GetPermissions(Role role)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            if (role is TRole dbRole)
            {
                return from p in dbRole.RolePermissions select p.Permission;
            }

            return (from p in securityContext.SecurityRoles.First(r => r.RoleName == role.RoleName).RolePermissions
                select new Permission
                {
                    //PermissionName = $"{(!p.Permission.IsGlobal ? p.Tenant.TenantName : "")}{p.Permission.PermissionName}"
                    PermissionName = p.Permission.PermissionName
                }).ToArray();
        }

        public bool PermissionScopeExists(string permissionScopeName)
        {
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser}));
            if (!isUser)
            {
                IQueryable<TUser> tenantUsers;
                var filteredLabels = (from ul in userLabels
                    where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                    select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                var appUsers = (from u in securityContext.ClientAppUsers join tu in securityContext.TenantUsers on u.TenantUserId equals tu.TenantUserId
                                select new {AppUser=u, UserId = tu.UserId}).Join(securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType)),m => m.UserId, UserId,(l,r) => l.AppUser);
                return (from d in appUsers
                        orderby d.TenantUser.Tenant.DisplayName
                        select new ScopeInfo { ScopeDisplayName = d.TenantUser.Tenant.DisplayName, ScopeName = d.TenantUser.Tenant.TenantName })
                    .ToArray();
            }

            return (from d in (from t in securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType))
                        .Join(securityContext.TenantUsers, UserId, u => u.UserId, (tu, tt) => new{tt.TenantUserId})
                        .Join(securityContext.UpwardsTenantUserRoles, l => l.TenantUserId, r => r.TenantUserId, (l,r)=>new{l.TenantUserId, r.OutermostLeafTenantId})
                        .Join(securityContext.Tenants, l => l.OutermostLeafTenantId, r => r.TenantId, (l,r)=> r)
                    select t).Distinct()
                orderby d.DisplayName
                select new ScopeInfo { ScopeDisplayName = d.DisplayName, ScopeName = d.TenantName }).ToArray();
        }

        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
        {
            IDisposable tmp = null;
            try
            {
                bool useCurrentTenant = string.IsNullOrEmpty(permissionScopeName) && securityContext.CurrentTenantId != null;
                if (!useCurrentTenant && !securityContext.Tenants.Any(n => n.TenantName == permissionScopeName))
                {
                    tmp = FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
                }

                var dt = DateTime.UtcNow;//DateTime.SpecifyKind(DateTime.UtcNow,DateTimeKind.Local);
                var raw = (from t in securityContext.Features
                    join a in securityContext.TenantFeatureActivations.Where(ta =>
                                ((!useCurrentTenant && ta.Tenant.TenantName == permissionScopeName) || (useCurrentTenant && ta.TenantId == securityContext.CurrentTenantId))
                                && (ta.ActivationStart == null || ta.ActivationStart <= dt)
                                && (ta.ActivationEnd == null || ta.ActivationEnd >= dt))
                            .GroupBy(g => new { g.FeatureId, g.Tenant.TenantName })
                            .Select(n => new { n.Key.FeatureId, n.Key.TenantName })
                        on t.FeatureId equals a.FeatureId into lfaj
                    from hoj in lfaj.DefaultIfEmpty()
                    select new { T = t, A = hoj.TenantName }).ToArray();

                /*EntityQueryable<Feature> mmp = (EntityQueryable<Feature>)raw;
                logger.LogDebug(mmp.DebugView.Query);*/
                return raw.Select(n => new Feature
                {
                    FeatureName = n.T.FeatureName,
                    FeatureDescription = n.T.FeatureDescription,
                    Enabled = n.T.Enabled || !string.IsNullOrEmpty(n.A)
                }).ToArray();
            }
            finally
            {
                tmp?.Dispose();
            }
        }

        public TimeZoneHelper GetTimeZoneHelper(string permissionScopeName)
        {
            var timezone = GetTimeZone(permissionScopeName);
            return new TimeZoneHelper(timezone);
        }

        public string Decrypt(string encryptedValue, string permissionScopeName)
        {
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName)
        {
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, initializationVector, salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            return securityContext.GetDecryptStreamForScope(baseStream, permissionScopeName, initializationVector, salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName)
        {
            return securityContext.GetDecryptStreamForScope(baseStream, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public string Encrypt(string value, string permissionScopeName)
        {
            return securityContext.EncryptForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName)
        {
            return securityContext.EncryptForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt)
        {
            return securityContext.EncryptForScope(value, permissionScopeName, out initializationVector, out salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt)
        {
            return securityContext.GetEncryptStreamForScope(baseStream, permissionScopeName, out initializationVector, out salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName)
        {
            return securityContext.GetEncryptStreamForScope(baseStream, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public string EncryptJsonObject(object value, string permissionScopeName)
        {
            return securityContext.EncryptJsonObjectForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public void Dispose()
        {
            OnDisposed();
        }

        protected virtual void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        protected TTrustConfig ConfigureTrustConfig(TTrustConfig trustConfig,
            [CallerMemberName] string callingMethod = null)
        {
            return ConfigureTrustConfigImpl(trustConfig, callingMethod);
        }

        protected abstract User SelectUser(TUser src);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(User user);

        protected abstract System.Linq.Expressions.Expression<Func<TUser, bool>> UserFilter(string[] userLabels, string authType);

        protected abstract IEnumerable<TUserRole> AllRoles(TUser user);

        protected abstract IEnumerable<CustomUserProperty<TUserId, TUser>> UserProps(TUser user);

        protected abstract Expression<Func<TUser, TUserId>> UserId { get; }

        protected abstract Expression<Func<UserTenantLevel<TUser>,TUserId>> IdOfUserLevelRecord { get; }

        protected abstract TTrustConfig ConfigureTrustConfigImpl(TTrustConfig trustConfig, string callingMethod);

        /// <summary>
        /// Estimats a claimData item from a given mapping and an original claim
        /// </summary>
        /// <param name="map">the mapping instruction for estimating a new claim</param>
        /// <param name="original">the original claim-value</param>
        /// <returns>a new claim that must be added to the currently logged on user</returns>
        private ClaimData TryGetClaim(AuthenticationClaimMapping map, ClaimData original)
        {
            try
            {
                return new ClaimData
                {
                    Type = original.FormatText(map.OutgoingClaimName),
                    ValueType = !string.IsNullOrEmpty(map.OutgoingValueType) ? original.FormatText(map.OutgoingValueType) : "",
                    Issuer = !string.IsNullOrEmpty(map.OutgoingIssuer) ? original.FormatText(map.OutgoingIssuer) : "",
                    OriginalIssuer = !string.IsNullOrEmpty(map.OutgoingOriginalIssuer) ? original.FormatText(map.OutgoingOriginalIssuer) : "",
                    Value = !string.IsNullOrEmpty(map.OutgoingClaimValue) ? original.FormatText(map.OutgoingClaimValue) : ""
                };
            }
            catch
            {
            }

            return null;
        }

        private TimeZoneInfo GetTimeZone(string permissionScopeName)
        {
            using (FullSecurityAccessHelper<TTrustConfig>.CreateForCaller(securityContext, securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = true })))
            {
                var t = securityContext.Tenants.First(n => n.TenantName == permissionScopeName);
                if (!string.IsNullOrEmpty(t.TimeZone))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(t.TimeZone);
                }
            }

            return TimeZoneInfo.Local;
        }

        private IQueryable<UserTenantLevel<TUser>> GetRawUserQuery()
        {
            int currentTenant = securityContext.CurrentTenantId ?? 0;
            var phase1 = (from t in securityContext.TenantUsers
                join j in securityContext.UpwardsTenantUserRoles on t.TenantUserId equals j.TenantUserId
                where securityContext.CurrentTenantId != null && j.OutermostLeafTenantId == securityContext.CurrentTenantId
                select new { t.UserId, t.User, j.ParentLevel });
            var phase2 = (from gj in phase1
                group gj by gj.UserId
                into g
                select new
                {
                    TenantId=currentTenant,
                    UserId = g.Key,
                    Level = g.Min(us => us.ParentLevel)
                });
            return (from p in phase2
                join t in securityContext.UpwardsTenantUserRoles on new { p.Level, p.UserId, p.TenantId } equals new
                    { Level = t.ParentLevel, t.UserId, TenantId=t.OutermostLeafTenantId }
                join tn in securityContext.TenantUsers on t.TenantUserId equals tn.TenantUserId
                select new UserTenantLevel<TUser>{User= tn.User, Level=t.ParentLevel, TenantId=t.OutermostLeafTenantId, RoleId=t.OutermostRoleId});
        }

        public event EventHandler Disposed;
    }
}
