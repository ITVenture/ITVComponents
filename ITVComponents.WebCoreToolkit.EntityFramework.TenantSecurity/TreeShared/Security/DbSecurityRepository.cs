using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Security;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.WebPlugins.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using ITVComponents.Cloning;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Feature = ITVComponents.WebCoreToolkit.Models.Feature;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Security
{
    public abstract class DbSecurityRepository<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> : ISecurityRepository
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
        where TUserProperty : CustomUserProperty<TUserId, TUser>, new()
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
        where TUser : class
        where TTenant : HierarchyTenant
        where TWebPlugin : HierarchyWebPlugin<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TWebPluginConstant : HierarchyWebPluginConstant<TTenant>
        where TWebPluginGenericParameter : HierarchyWebPluginGenericParameter<TTenant, TWebPlugin, TWebPluginGenericParameter>
        where TSequence : Sequence<TTenant>
        where TTenantSetting : HierarchyTenantSetting<TTenant>
        where TTenantFeatureActivation : TenantFeatureActivation<TTenant>
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTrustConfig : HierarchyTenantContextSecurityTrustConfig<TTrustConfig>, new()
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TExternalOAuthService : HierarchyExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceState : HierarchyExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
        where TExternalOAuthServiceTenantLogin : HierarchyExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
    {
        private readonly IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext;
        private readonly ISecurityAccessProvider securityAccessProvider;
        private readonly IOptions<ExternalOAuthServiceBufferingOptions> bufferOptions;
        private readonly ILogger logger;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ExternalOAuthServiceBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>>
            bufferedServices= new ConcurrentDictionary<string, ConcurrentDictionary<string, ExternalOAuthServiceBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>>();

        // Per-instance memoization for IsAuthenticated. The repository is scoped together with the
        // DbContext (per circuit / per request) so each entry is naturally scoped to the same lifetime.
        // Needed because Blazor Server schedules async OnAfterRenderAsync callbacks for sibling
        // components in parallel; both can land in HasPermission → IsAuthenticated which triggers a
        // GetRawUserQuery EF query against the *same* scoped DbContext while another query is
        // still enumerating its DataReader → "A second operation was started on this context".
        private readonly ConcurrentDictionary<string, bool> isAuthenticatedCache = new();
        private readonly ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal changeSignal;
        private DateTime authCacheStampUtc = DateTime.UtcNow;

        // Used to spin up a dedicated, short-lived context instance for scope resolution (see GetEligibleScopes)
        // so it can never collide with the shared circuit-scoped securityContext. May be null for the legacy
        // (factory-without-services) path, in which case scope resolution falls back to the shared instance.
        private readonly IServiceProvider services;

        protected DbSecurityRepository(IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext, ISecurityAccessProvider securityAccessProvider, IOptions<ExternalOAuthServiceBufferingOptions> bufferOptions,
            ILogger logger, ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal changeSignal = null, IServiceProvider services = null)
        {
            this.securityContext = securityContext;
            this.securityAccessProvider = securityAccessProvider;
            this.bufferOptions = bufferOptions;
            this.logger = logger;
            this.changeSignal = changeSignal;
            this.services = services;
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
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
                return (from r in securityContext.SecurityRoles where r.TenantId == securityContext.CurrentTenantId select r).ToList<Role>();
            }
        }

        public ICollection<Permission> Permissions
        {
            get
            {
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
                return (from p in securityContext.Permissions where p.TenantId == null || p.TenantId == securityContext.CurrentTenantId select p).ToList<Permission>();
            }
        }

        public IEnumerable<Role> GetRoles(User user)
        {
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = true});
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope)
        {
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
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
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
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
                    retVal = JsonHelper.FromJsonString<T>(tmpVal, SerializationTypingMode.StaticTyping);
                }
            }

            return retVal;
        }

        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value)
        {
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
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
                stringVal = JsonHelper.ToJson(value, SerializationTypingMode.StaticTyping, null);
            }

            return SetCustomProperty(user, propertyName, propertyType, stringVal);
        }

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
        {
            InvalidateAuthCacheIfStale();
            var t = securityContext.CurrentTenantId;
            var cacheKey = BuildAuthCacheKey(userLabels, userAuthenticationType, t, scope: null);
            // ConcurrentDictionary.GetOrAdd serializes concurrent first-time lookups by key:
            // only one caller runs the EF query, others block on the same Lazy<>.Value.
            return isAuthenticatedCache.GetOrAdd(cacheKey, _ => IsAuthenticatedCore(userLabels, userAuthenticationType, t));
        }

        /// <summary>
        /// Clears the per-instance IsAuthenticated memoization when a security-relevant entity changed since
        /// the cache was last filled, so revoked/granted access takes effect within a live circuit. No-op when
        /// no change-signal is registered (EntityWriteTracker inactive).
        /// </summary>
        private void InvalidateAuthCacheIfStale()
        {
            if (changeSignal != null &&
                changeSignal.GetLastChange(ITVComponents.WebCoreToolkit.Caching.EntityChangeTopics.Security) > authCacheStampUtc)
            {
                isAuthenticatedCache.Clear();
                authCacheStampUtc = DateTime.UtcNow;
            }
        }

        private bool IsAuthenticatedCore(string[] userLabels, string userAuthenticationType, int? t)
        {
            // Shadow the shared field with a dedicated, short-lived instance so this render-path read can't collide
            // with parallel Blazor lifecycle callbacks on the circuit-scoped context. The whole body then transparently
            // uses the detached instance; GetRawUserQuery gets it explicitly via readCtx.
            var __det = services != null ? CreateDetachedContext() : null;
            var securityContext = __det ?? this.securityContext;
            try
            {
                if (t != null)
                {
                    var ti = t.Value;
                    IQueryable<UserTenantLevel<TUser>> tenantUsers;
                    var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
                    using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                        new TTrustConfig { HideGlobals = false, IncludeParentTree = isUser, ShowAllTenants = false });
                    if (isUser)
                    {
                        tenantUsers = GetRawUserQuery(out _, userLabels, securityContext.CurrentTenantName, securityContext);
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
            finally
            {
                (__det as IDisposable)?.Dispose();
            }
        }

        public bool IsAuthenticated(string[] userLabels, string forScope, string userAuthenticationType)
        {
            InvalidateAuthCacheIfStale();
            var cacheKey = BuildAuthCacheKey(userLabels, userAuthenticationType, tenantId: null, scope: forScope);
            return isAuthenticatedCache.GetOrAdd(cacheKey, _ => IsAuthenticatedScopedCore(userLabels, forScope, userAuthenticationType));
        }

        private bool IsAuthenticatedScopedCore(string[] userLabels, string forScope, string userAuthenticationType)
        {
            var __det = services != null ? CreateDetachedContext() : null;
            var securityContext = __det ?? this.securityContext;
            try
            {
                var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    new TTrustConfig { HideGlobals = false, IncludeParentTree = isUser, ShowAllTenants = true });
                var t = securityContext.Tenants.FirstOrDefault(n => n.TenantName == forScope)?.TenantId;
                if (t != null)
                {
                    var ti = t.Value;
                    IQueryable<UserTenantLevel<TUser>> tenantUsers;

                    if (isUser)
                    {
                        tenantUsers = GetRawUserQuery(out _, userLabels, forScope, securityContext);
                    }
                    else
                    {
                        var filteredLabels = (from ul in userLabels
                            where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                            select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                        var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ti);
                        tenantUsers = appUsers
                            .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                            .Select(n => new UserTenantLevel<TUser> { User = n.TenantUser.User, TenantId = ti, Level = 1 });
                    }

                    return tenantUsers.Select(n => n.User).Any(UserFilter(userLabels, userAuthenticationType));
                }

                return false;
            }
            finally
            {
                (__det as IDisposable)?.Dispose();
            }
        }

        private static string BuildAuthCacheKey(string[] userLabels, string userAuthenticationType, int? tenantId, string scope)
        {
            // Stable, allocation-cheap cache key. userLabels can be null when only the OAuth-flow runs.
            var labels = userLabels is null || userLabels.Length == 0 ? "-" : string.Join("", userLabels);
            return $"{labels}{userAuthenticationType ?? "-"}{tenantId?.ToString() ?? "-"}{scope ?? "-"}";
        }

        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType,
            CustomUserPropertyType propertyType)
        {
            var isUser = userLabels.All(n => string.IsNullOrEmpty(n) || !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser});
            IQueryable<TUser> tenantUsers;
            if (isUser)
            {
                tenantUsers = securityContext.Users; //GetRawUserQuery(out _);
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
                    .Select(n => n.TenantUser.User);
            }
            return (from u in tenantUsers.Where(UserFilter(userLabels, userAuthenticationType))
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
            using var tmp = securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser}));
            IQueryable<UserTenantLevel<TUser>> tenantUsers;
            if (isUser)
            {
                tenantUsers = GetRawUserQuery(out _, userLabels);
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
            using var tmp = securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false }));
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
            var tmpRr = (from r in AllRoles(securityContext.Users.First(UserFilter(user)))
                select new
                {
                    rp = r.Role.RolePermissions,
                    grp = r.Role.PermittedGlobalRoles.SelectMany(gpr => gpr.GlobalRole.RolePermissions)
                }).ToArray();
            var preRet = tmpRr
                .SelectMany(rp => rp.rp).Select(p => new Permission
                {
                    //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                    PermissionName = p.Permission.PermissionName
                }).Union(tmpRr.SelectMany(grp => grp.grp).Select(p => new Permission
                {
                    //PermissionName = $"{(!p.Permission.IsGlobal?p.Tenant.TenantName:"")}{p.Permission.PermissionName}"
                    PermissionName = p.Permission.PermissionName
                })).Distinct();

            var rett = preRet.ToArray();
            return rett;
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
        {
            // Render-path read on a dedicated context (shadow of the shared field) -> collision-free.
            var __det = services != null ? CreateDetachedContext() : null;
            var securityContext = __det ?? this.securityContext;
            try
            {
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                ConfigureTrustConfig(new()
                    { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser }));

            if (isUser)
            {
                var preFiltered = GetRawUserQuery(out var currentTenant, userLabels, readCtx: securityContext);
                var tmptu = securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType)).Join(
                    preFiltered,
                    UserId, IdOfUserLevelRecord, (l, r) => new { r.Level, r.TenantId, r.RoleId, User=l });
                var pr = (from t in tmptu
                    join r in securityContext.RolePermissions on t.RoleId equals r.RoleId
                    join p in securityContext.Permissions on r.PermissionId equals p.PermissionId
                    select new { t.TenantId, t.User, t.Level, Permission=p })
                    .Where(n => n.TenantId == currentTenant)
                    .Select(n => n.Permission).Union(from t in tmptu
                        join rj in securityContext.GlobalToLocalRoles on t.RoleId equals rj.LocalRoleId
                        join rp in securityContext.GlobalRolePermissions on rj.GlobalRoleId equals rp.GlobalRoleId
                        join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                                                     where t.TenantId == currentTenant
                                                     select p).Distinct();
                var parr = pr.ToArray();
                return parr;
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
                join r in securityContext.SecurityRoles.Include(n => n.RolePermissions).ThenInclude(rp => rp.Permission) 
                        .Include(n => n.PermittedGlobalRoles).ThenInclude(pgr => pgr.GlobalRole).ThenInclude(gr => gr.RolePermissions).ThenInclude(grp => grp.Permission)
                    on new { RoleId = ur.RoleId.Value, tr.TenantId } equals new { r.RoleId, r.TenantId }
                           select r
                //join rp in securityContext.RolePermissions /*.Where(n => n.RoleId != null)*/
                    //on new { r.RoleId, r.TenantId } equals new { RoleId = rp.RoleId, rp.TenantId }
                //join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                //join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                /*select new Permission
                {
                    //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                    PermissionName = p.PermissionName
                }*/);
            var qLoc = permRaw.SelectMany(n => n.RolePermissions.Select(rp => new Permission
            {
                PermissionName = rp.Permission.PermissionName
            }));
            var qGlob = permRaw.SelectMany(n => n.PermittedGlobalRoles.SelectMany(gr => gr.GlobalRole.RolePermissions))
                .Select(gp => new Permission
                {
                    PermissionName = gp.Permission.PermissionName
                });
            var permRaw2 = qLoc.Union(qGlob).Distinct();
            var permRawArr = permRaw2.ToArray();
            if (preFilteredPerms != null)
            {
                permRawArr = (from t in permRawArr join p in preFilteredPerms on t.PermissionName equals p select t)
                    .ToArray();
            }

            return permRawArr;
            }
            finally
            {
                (__det as IDisposable)?.Dispose();
            }
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string forScope, string userAuthenticationType)
        {
            var __det = services != null ? CreateDetachedContext() : null;
            var securityContext = __det ?? this.securityContext;
            try
            {
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                ConfigureTrustConfig(new()
                { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = isUser }));

            if (isUser)
            {
                var preFiltered = GetRawUserQuery(out var currentTenantId,userLabels, forScope, securityContext);
                var tmptu = securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType)).Join(
                    preFiltered,
                    UserId, IdOfUserLevelRecord, (l, r) => new { r.Level, r.TenantId, r.RoleId, User = l });
                var pr = (from t in tmptu
                        join r in securityContext.RolePermissions on t.RoleId equals r.RoleId
                        join p in securityContext.Permissions on r.PermissionId equals p.PermissionId
                        select new { t.TenantId, t.User, t.Level, Permission = p })
                    .Where(n => n.TenantId == currentTenantId)
                    .Select(n => n.Permission).Union(from t in tmptu
                        join rj in securityContext.GlobalToLocalRoles on t.RoleId equals rj.LocalRoleId
                        join rp in securityContext.GlobalRolePermissions on rj.GlobalRoleId equals rp.GlobalRoleId
                        join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                        where t.TenantId == currentTenantId
                        select p).Distinct();
                var parr = pr.ToArray();
                return parr;
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
                           join r in securityContext.SecurityRoles.Include(n => n.RolePermissions).ThenInclude(rp => rp.Permission)
                               .Include(n => n.PermittedGlobalRoles).ThenInclude(pgr => pgr.GlobalRole).ThenInclude(gr => gr.RolePermissions).ThenInclude(grp => grp.Permission)
                               on new { RoleId = ur.RoleId.Value, tr.TenantId } equals new { r.RoleId, r.TenantId }
                           select r
                           //join rp in securityContext.RolePermissions /*.Where(n => n.RoleId != null)*/
                           //    on new { r.RoleId, r.TenantId } equals new { RoleId = rp.RoleId, rp.TenantId }
                           //join rt in securityContext.Tenants on rp.TenantId equals rt.TenantId
                           //join p in securityContext.Permissions on rp.PermissionId equals p.PermissionId
                           /*select new Permission
                           {
                               //PermissionName = p.PermissionName != rt.TenantName?$"{(!p.IsGlobal?rt.TenantName:"")}{p.PermissionName}":p.PermissionName
                               PermissionName = p.PermissionName
                           }*/).Distinct().ToArray();

            var qLoc = permRaw.SelectMany(n => n.RolePermissions.Select(rp => new Permission
            {
                PermissionName = rp.Permission.PermissionName
            }));
            var qGlob = permRaw.SelectMany(n => n.PermittedGlobalRoles.SelectMany(gr => gr.GlobalRole.RolePermissions))
                .Select(gp => new Permission
                {
                    PermissionName = gp.Permission.PermissionName
                });
            var permRaw2 = qLoc.Union(qGlob).Distinct();
            var permRawArr = permRaw2.ToArray();
            if (preFilteredPerms != null)
            {
                permRawArr = (from t in permRawArr join p in preFilteredPerms on t.PermissionName equals p select t)
                    .ToArray();
            }

            return permRawArr;
            }
            finally
            {
                (__det as IDisposable)?.Dispose();
            }
        }

        public IEnumerable<Permission> GetPermissions(Role role)
        {
            using var tmp = securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
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
            using var tmp = securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            // Resolve eligible scopes on a dedicated, short-lived context instance instead of the shared
            // circuit-scoped securityContext. This runs synchronously from CurrentTenantId/PermissionPrefix while
            // parallel Blazor lifecycle callbacks may have an operation open on the same shared instance ->
            // "a second operation was started on this context instance". A detached instance bound to the same
            // IPermissionScope/IContextUserProvider is correctly scoped yet collision-free (mirrors the navigation
            // builder, f53cae61). The trust elevation below targets the detached instance; the trust lookup itself
            // is served from the access-provider cache, so no query hits the shared context. Falls back to the
            // shared instance only on the legacy path where no IServiceProvider was injected.
            var ownsCtx = services != null;
            var ctx = ownsCtx ? CreateDetachedContext() : securityContext;
            try
            {
                var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
                using var tmp = securityAccessProvider.CreateForCaller(ctx, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = false}));
                if (!isUser)
                {
                    var appUsers = (from u in ctx.ClientAppUsers join tu in ctx.TenantUsers on u.TenantUserId equals tu.TenantUserId
                                    select new {AppUser=u, UserId = tu.UserId}).Join(ctx.Users.Where(UserFilter(userLabels, userAuthenticationType)),m => m.UserId, UserId,(l,r) => l.AppUser);
                    return (from d in appUsers
                            orderby d.TenantUser.Tenant.DisplayName
                            select new ScopeInfo { ScopeDisplayName = d.TenantUser.Tenant.DisplayName, ScopeName = d.TenantUser.Tenant.TenantName })
                        .ToArray();
                }

                return (from d in (from t in ctx.Users.Where(UserFilter(userLabels, userAuthenticationType))
                            .Join(ctx.TenantUsers, UserId, u => u.UserId, (tu, tt) => new{tt.TenantUserId, tt.TenantId})
                            .Join(ctx.GetUpwardsTenantUserRoles(userLabels,(string)null), l => l.TenantUserId, r => r.TenantUserId, (l,r)=>new{l.TenantUserId, l.TenantId, r.OutermostLeafTenantId})
                            .Join(ctx.Tenants, l => l.OutermostLeafTenantId, r => r.TenantId, (l,r)=> new {l,r})
                        select new {t.r.TenantId, t.r.TenantName, t.r.DisplayName, DirectlyAssigned=t.l.TenantId==t.r.TenantId}).Distinct()
                    orderby d.DisplayName
                    select new ScopeInfo { ScopeDisplayName = d.DisplayName, ScopeName = d.TenantName, AccessMode = d.DirectlyAssigned?ScopeAccessMode.Direct:ScopeAccessMode.Inherited}).ToArray();
            }
            finally
            {
                if (ownsCtx)
                {
                    (ctx as IDisposable)?.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates a fresh, DI-bound context instance (same IPermissionScope/IContextUserProvider as the shared
        /// one, but a separate DbContext) for collision-free scope resolution. The caller owns and disposes it.
        /// <see cref="ActivatorUtilities"/> selects the richest resolvable (dependency-injected) constructor.
        /// </summary>
        private IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> CreateDetachedContext()
            => (IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>)ActivatorUtilities.CreateInstance(services, securityContext.GetType());

        /// <summary>
        /// Runs a read on a dedicated, short-lived context instance (collision-free w.r.t. the shared circuit
        /// context) and disposes it afterwards. The result MUST be fully materialized inside <paramref name="read"/>
        /// (do not return a lazy IQueryable). Falls back to the shared context on the legacy no-IServiceProvider path.
        /// </summary>
        private TResult ReadDetached<TResult>(Func<IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>, TResult> read)
        {
            if (services == null)
            {
                return read(securityContext);
            }

            var ctx = CreateDetachedContext();
            try
            {
                return read(ctx);
            }
            finally
            {
                (ctx as IDisposable)?.Dispose();
            }
        }

        public IEnumerable<Feature> GetFeatures(string permissionScopeName)
            => ReadDetached(ctx =>
            {
                IDisposable tmp = null;
                try
                {
                    bool useCurrentTenant = string.IsNullOrEmpty(permissionScopeName) && ctx.CurrentTenantId != null;
                    int? tenantToUse = null;
                    if (!useCurrentTenant)
                    {
                        tmp = securityAccessProvider.CreateForCaller(ctx, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false }));
                    }
                    else
                    {
                        tenantToUse = ctx.CurrentTenantId;
                    }

                    var dt = DateTime.UtcNow;//DateTime.SpecifyKind(DateTime.UtcNow,DateTimeKind.Local);
                    var raw = (from t in ctx.Features
                        join a in ctx.TenantFeatureActivations.Where(ta =>
                                    ((!useCurrentTenant && ta.Tenant.TenantName == permissionScopeName) || (useCurrentTenant && ta.TenantId == tenantToUse))
                                    && (ta.ActivationStart == null || ta.ActivationStart <= dt)
                                    && (ta.ActivationEnd == null || ta.ActivationEnd >= dt))
                                .GroupBy(g => new { g.FeatureId, g.Tenant.TenantName })
                                .Select(n => new { n.Key.FeatureId, n.Key.TenantName })
                            on t.FeatureId equals a.FeatureId into lfaj
                        from hoj in lfaj.DefaultIfEmpty()
                        select new { T = t, A = hoj.TenantName }).ToArray();

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
            });

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

        public Permission[] GetKnownPermissions(string permissionScope)
            => ReadDetached(ctx =>
            {
                using var tmp = securityAccessProvider.CreateForCaller(
                    ctx,
                    new TTrustConfig { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = false });
                return (from p in ctx.Permissions
                    where p.TenantId == null || p.Tenant.TenantName == permissionScope
                    select new Permission { PermissionName = p.PermissionName }).ToArray();
            });

        public ExternalServiceConnection GetExternalService(string name, bool decryptSecret = false)
        {
            var tmp = GetExternalOAuthService(name, out _, out _, out var tenantName, true).Copy();
            if (decryptSecret && !string.IsNullOrEmpty(tmp.ClientSecret))
            {
                if (tmp.Global)
                {
                    tmp.ClientSecret = tmp.ClientSecret.Decrypt();
                }
                else
                {
                    tmp.ClientSecret = Decrypt(tmp.ClientSecret, tenantName);
                }
            }

            return tmp;
        }

        public void PrepareExternalServiceConnect(OAuthState oAuthState)
        {
            var tmp = GetExternalOAuthService(oAuthState.ConnectionName, out var serviceId, out var tenantId, out _, true);
            if (tenantId != securityContext.CurrentTenantId && tenantId != null)
            {
                throw new InvalidOperationException("Can only configure a connection for the active tenant");
            }

            if (securityContext.CurrentTenantId != null && serviceId != null)
            {
                var state = new TExternalOAuthServiceState
                {
                    TenantId = securityContext.CurrentTenantId.Value,
                    OAuthServiceId = serviceId.Value,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                    State = oAuthState.State,
                    CodeVerifier = oAuthState.CodeVerifier
                };

                securityContext.ExternalOAuthServiceStates.Add(state);
                securityContext.SaveChanges();
            }
        }

        public OAuthState GetOAuthRequest(string connectionName, string state)
        {
            var now = DateTime.UtcNow;
            bool switchRequired = false;

            LogEnvironment.LogDebugEvent("Looking up all services", LogSeverity.Warning);
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = false });
            var bufferedState = securityContext.ExternalOAuthServiceStates.Include(n => n.Connection)
                .Include(n => n.Tenant)
                .FirstOrDefault(n =>
                    n.Connection.CalculatedUniqueServiceName == connectionName && n.State == state && !n.Used &&
                    n.ExpiresAt > now);

            if (bufferedState != null)
            {
                switchRequired = bufferedState.TenantId != securityContext.CurrentTenantId;
                bufferedState.Used = true;
                securityContext.SaveChanges();

                string explicitTenant = null;
                if (switchRequired)
                {
                    explicitTenant = bufferedState.Tenant.TenantName;
                }

                return new OAuthState
                {
                    ConnectionName = bufferedState.Connection.CalculatedUniqueServiceName,
                    ExpiresAt = bufferedState.ExpiresAt,
                    State = bufferedState.State,
                    CodeVerifier = bufferedState.CodeVerifier,
                    ScopeSwitchRequired = switchRequired,
                    ExplicitScope = explicitTenant
                };
            }

            return null;
        }

        public void StoreExternalServiceToken(string connectionName, TranslatedTokenResponse token)
        {
            var tmp = GetExternalOAuthService(connectionName, out var serviceId, out var tenantId, out var tenantName, true);
            if (tenantId != securityContext.CurrentTenantId && tenantId != null)
            {
                throw new InvalidOperationException("Can only configure a connection for the active tenant");
            }

            var login = securityContext.ExternalOAuthServiceTenantLogins.FirstOrDefault(n =>
                n.OAuthServiceId == serviceId && n.TenantId == securityContext.CurrentTenantId);
            var encToken = new TranslatedTokenResponse()
            {
                ExpiresAt = token.ExpiresAt,
                Scope = token.Scope,
                RefreshToken = Encrypt(token.RefreshToken, securityContext.CurrentTenantName),
                AccessToken = Encrypt(token.AccessToken, securityContext.CurrentTenantName),
                TokenType=token.TokenType
            };

            if (login == null)
            {
                login = new TExternalOAuthServiceTenantLogin
                {
                    TenantId = securityContext.CurrentTenantId.Value,
                    OAuthServiceId = serviceId.Value,
                    Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping)
                };
                securityContext.ExternalOAuthServiceTenantLogins.Add(login);
            }
            else
            {
                login.Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping);
                login.Revoked = false;
            }

            securityContext.SaveChanges();
        }

        public TranslatedTokenResponse GetBufferedToken(string connectionName, bool forRevoke, bool throwIfNull, out ExternalServiceConnection connectionInfo, out Action<TranslatedTokenResponse> updateToken)
        {
            using var tmpSecurity = securityAccessProvider.CreateForCaller(securityContext,
                new TTrustConfig { HideGlobals = false, IncludeParentTree = true, ShowAllTenants = false });
            var svc = GetExternalOAuthService(connectionName, out var serviceId, out var tenantId, out var tenantName, false).Copy();
            if (!string.IsNullOrEmpty(svc.ClientSecret))
            {
                if (svc.Global)
                {
                    svc.ClientSecret = svc.ClientSecret.Decrypt();
                }
                else
                {
                    svc.ClientSecret = Decrypt(svc.ClientSecret, tenantName);
                }
            }

            connectionInfo = svc;
            if (serviceId != null)
            {
                var login = GetExternalOAuthServiceLogin(serviceId.Value,false);
                var targetTenant = login?.Tenant.TenantName ?? tenantName;
                if (login is { Revoked: false } && (!forRevoke || login.TenantId == securityContext.CurrentTenantId))
                {
                    var tmp = JsonHelper.FromJsonString<TranslatedTokenResponse>(login.Token,
                        SerializationTypingMode.StaticTyping);
                    var token = new TranslatedTokenResponse
                    {
                        AccessToken = Decrypt(tmp.AccessToken, targetTenant),
                        ExpiresAt = tmp.ExpiresAt,
                        RefreshToken = Decrypt(tmp.RefreshToken, targetTenant),
                        Scope = tmp.Scope,
                        TokenType = tmp.TokenType
                    };

                    if (!forRevoke)
                    {
                        updateToken = newToken =>
                        {
                            var encToken = new TranslatedTokenResponse
                            {
                                Scope = newToken.Scope,
                                AccessToken = Encrypt(newToken.AccessToken, targetTenant),
                                ExpiresAt = newToken.ExpiresAt,
                                RefreshToken = Encrypt(newToken.RefreshToken, targetTenant),
                                TokenType = newToken.TokenType
                            };

                            login.Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping);
                            securityContext.SaveChanges();
                        };
                    }
                    else
                    {
                        updateToken = _ => { };
                        login.Revoked = true;
                        login.Token = "{}";
                        securityContext.SaveChanges();
                    }

                    return token;
                }

                if (!throwIfNull)
                {
                    updateToken = newToken =>
                    {
                        var encToken = new TranslatedTokenResponse
                        {
                            Scope = newToken.Scope,
                            AccessToken = Encrypt(newToken.AccessToken, targetTenant),
                            ExpiresAt = newToken.ExpiresAt,
                            RefreshToken = Encrypt(newToken.RefreshToken, targetTenant),
                            TokenType = newToken.TokenType
                        };

                        if (login != null)
                        {
                            login.Revoked = false;
                            login.Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping);
                        }
                        else
                        {
                            login = new TExternalOAuthServiceTenantLogin
                            {
                                TenantId = tenantId ?? securityContext.CurrentTenantId.Value,
                                Revoked = false,
                                OAuthServiceId = serviceId.Value,
                                Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping)
                            };

                            securityContext.ExternalOAuthServiceTenantLogins.Add(login);

                        }

                        securityContext.SaveChanges();
                    };

                    return null;
                }
            }

            throw new InvalidOperationException($"No appropriate Token found for {connectionName}");
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

        protected virtual ExternalServiceConnection GetExternalOAuthService(string name, out int? externalOAuthServiceId, out int? tenantId, out string? tenantName, bool useFullAccess)
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants && ServiceBuffered(name, out var bufferInfo))
            {
                externalOAuthServiceId = bufferInfo.ExternalOAuthServiceId;
                tenantId = bufferInfo.TenantId;
                tenantName = bufferInfo.TenantName;
                return bufferInfo.Service;
            }

            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                IDisposable tmp = null;
                if (useFullAccess)
                {
                    tmp = securityAccessProvider.CreateForCaller(securityContext,
                        new TTrustConfig { HideGlobals = false, IncludeParentTree = true, ShowAllTenants = false });
                }

                try
                {
                    var phase1 = from p in securityContext.UpwardsTenantTreeView
                        join pin in securityContext.ExternalOAuthServices on p.ParentTenantId equals pin.TenantId
                        where p.OutermostLeafTenantId == securityContext.CurrentTenantId.Value &&
                              (p.ParentLevel == 1 || pin.Inheritable)
                        select new { pin.UniqueConnectionName, p.OutermostLeafTenantId, p.ParentLevel };
                    var phase2 = from gj in phase1
                        group gj by new { gj.UniqueConnectionName, gj.OutermostLeafTenantId }
                        into g
                        select new
                        {
                            TenantId = g.Key.OutermostLeafTenantId,
                            UniqueConnectionName = g.Key.UniqueConnectionName,
                            Level = g.Min(n => n.ParentLevel)
                        };
                    var phase3 = from p in phase2
                        join t in securityContext.UpwardsTenantTreeView on new { p.TenantId, p.Level } equals
                            new { TenantId = t.OutermostLeafTenantId, Level = t.ParentLevel }
                        join pg in securityContext.ExternalOAuthServices.Include(n => n.Tenant) on new
                                { p.UniqueConnectionName, TenantId = t.ParentTenantId }
                            equals new { pg.UniqueConnectionName, TenantId = pg.TenantId.Value }
                        where pg.UniqueConnectionName == name || pg.CalculatedUniqueServiceName == name
                                 select pg;
                    var pi = phase3.FirstOrDefault() ??
                             securityContext.ExternalOAuthServices.Include(n => n.Tenant)
                                 .FirstOrDefault(n => n.TenantId == null && (n.UniqueConnectionName == name || n.CalculatedUniqueServiceName == name));
                    externalOAuthServiceId = pi?.OAuthServiceId;
                    tenantId = pi?.TenantId;
                    tenantName = pi?.Tenant?.TenantName;
                    return TryRegisterService(name, pi);
                }
                finally
                {
                    tmp?.Dispose();
                }
            }

            throw new InvalidOperationException("Invalid DB-Access mode");
        }

        protected virtual TExternalOAuthServiceTenantLogin GetExternalOAuthServiceLogin(int oauthServiceId, bool useFullAccess)
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants)
            {
                IDisposable tmp = null;
                if (useFullAccess)
                {
                    tmp = securityAccessProvider.CreateForCaller(securityContext,
                        new TTrustConfig { HideGlobals = false, IncludeParentTree = true, ShowAllTenants = false });
                }

                try
                {
                    var phase1 = from p in securityContext.UpwardsTenantTreeView
                        join pin in securityContext.ExternalOAuthServiceTenantLogins.Include(n => n.OAuthService) on p
                            .ParentTenantId equals pin.TenantId
                        where p.OutermostLeafTenantId == securityContext.CurrentTenantId.Value && (p.ParentLevel == 1 ||
                            pin.OAuthService.Inheritable || pin.OAuthService.TenantId == null)
                        select new { pin.OAuthServiceId, p.OutermostLeafTenantId, p.ParentLevel };
                    var phase2 = from gj in phase1
                        group gj by new { gj.OAuthServiceId, gj.OutermostLeafTenantId }
                        into g
                        select new
                        {
                            TenantId = g.Key.OutermostLeafTenantId,
                            OAuthServiceId = g.Key.OAuthServiceId,
                            Level = g.Min(n => n.ParentLevel)
                        };
                    var phase3 = from p in phase2
                        join t in securityContext.UpwardsTenantTreeView on new { p.TenantId, p.Level } equals
                            new { TenantId = t.OutermostLeafTenantId, Level = t.ParentLevel }
                        join pg in securityContext.ExternalOAuthServiceTenantLogins.Include(n => n.Tenant) on new
                                { p.OAuthServiceId, TenantId = t.ParentTenantId }
                            equals new { pg.OAuthServiceId, TenantId = pg.TenantId }
                        where pg.OAuthServiceId == oauthServiceId
                        select pg;
                    var pi = phase3.FirstOrDefault();
                    return pi;
                }
                finally
                {
                    tmp?.Dispose();
                }
            }

            throw new InvalidOperationException("Invalid DB-Access mode");
        }

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
            using (securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = true })))
            {
                var t = securityContext.Tenants.First(n => n.TenantName == permissionScopeName);
                if (!string.IsNullOrEmpty(t.TimeZone))
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(t.TimeZone);
                }
            }

            return TimeZoneInfo.Local;
        }

        private IQueryable<UserTenantLevel<TUser>> GetRawUserQuery(out int currentTenantId, string[] userLabels, string forTenant = null,
            IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> readCtx = null)
        {
            // readCtx lets render-path callers run this on a dedicated context (collision-free). Defaults to the
            // shared context for all other (sequential) callers.
            var sc = readCtx ?? securityContext;
            int currentTenant = 0;
            if (string.IsNullOrEmpty(forTenant))
            {
                currentTenant = sc.CurrentTenantId ?? 0;
            }
            else
            {
                currentTenant = sc.Tenants.FirstOrDefault(n => n.TenantName == forTenant)?.TenantId ?? 0;
            }

            currentTenantId = currentTenant;
            var phase1 = (from t in sc.TenantUsers
                join j in sc.GetUpwardsTenantUserRoles(userLabels,forTenant) on t.TenantUserId equals j.TenantUserId
                where j.OutermostLeafTenantId == currentTenant
                select new { t.UserId, t.User, j.ParentLevel });
            var phase2 = (from gj in phase1
                group gj by gj.UserId
                into g
                select new
                {
                    TenantId = currentTenant,
                    UserId = g.Key,
                    Level = g.Min(us => us.ParentLevel)
                });
            return (from p in phase2
                join t in sc.GetUpwardsTenantUserRoles(userLabels, forTenant) on new { p.Level, p.UserId, p.TenantId } equals new
                    { Level = t.ParentLevel, t.UserId, TenantId = t.OutermostLeafTenantId }
                join tn in sc.TenantUsers on t.TenantUserId equals tn.TenantUserId
                select new UserTenantLevel<TUser>
                {
                    User = tn.User, Level = t.ParentLevel, TenantId = t.OutermostLeafTenantId,
                    RoleId = t.OutermostRoleId
                });
        }

        private ExternalServiceConnection TryRegisterService(string uniqueName, TExternalOAuthService serviceData)
        {
            var dc = bufferedServices.GetOrAdd(securityContext.CurrentTenantName,
                n => new ConcurrentDictionary<string, ExternalOAuthServiceBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>());
            var svcData = serviceData != null
                ? serviceData
                    .ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState,
                        TExternalOAuthServiceTenantLogin>()
                : null;
            /*if (!string.IsNullOrEmpty(svcData?.ClientSecret))
            {
                if (svcData.Global)
                {
                    svcData.ClientSecret = svcData.ClientSecret.Decrypt();
                }
                else
                {
                    svcData.ClientSecret = Decrypt(svcData.ClientSecret, serviceData.Tenant?.TenantName);
                }
            }*/

            var svc = new ExternalOAuthServiceBufferInfo() /*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/
            {
                Created = DateTime.Now,
                Service = svcData,
                ExternalOAuthServiceId = serviceData?.OAuthServiceId,
                TenantId = serviceData?.TenantId,
                TenantName = serviceData?.Tenant?.TenantName
            };
            dc.TryAdd(uniqueName, svc);
            dc.TryAdd(svc.Service.GlobalUniqueConnectionName, svc);

            return serviceData.ToServiceDefinition<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>();
        }

        private bool ServiceBuffered(string name, out ExternalOAuthServiceBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/ bufferInfo)
        {
            var dc = bufferedServices.GetOrAdd(securityContext.CurrentTenantName,
                n => new ConcurrentDictionary<string, ExternalOAuthServiceBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/>());
            var retVal = dc.TryGetValue(name, out bufferInfo);
            var bufferConfig = bufferOptions.Value;
            if (retVal && bufferConfig.BufferDuration != 0 &&
                DateTime.Now.Subtract(bufferInfo.Created).TotalSeconds > bufferConfig.BufferDuration)
            {
                dc.Remove(name, out _);
                bufferInfo = null;
                return false;
            }

            return retVal;
        }

        public event EventHandler Disposed;
    }
}
