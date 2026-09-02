using ITVComponents.Formatting;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.Scripting.CScript.Core;
using ITVComponents.Security;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
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
        // Per-operation factory for the security context (Blazor-safe: a fresh, short-lived context per call instead
        // of a shared circuit-scoped one). Each public operation leases one context and threads it through its
        // private helpers, so the whole operation runs on a single, collision-free context instance.
        private readonly IToolkitContextFactory contextFactory;
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

        // Preserved for compatibility with existing registrations (was previously used to spin up a dedicated context
        // for scope resolution; that role is now served by the per-operation IToolkitContextFactory). May be null.
        private readonly IServiceProvider services;

        protected DbSecurityRepository(IToolkitContextFactory contextFactory, ISecurityAccessProvider securityAccessProvider, IOptions<ExternalOAuthServiceBufferingOptions> bufferOptions,
            ILogger logger, ITVComponents.WebCoreToolkit.Caching.IEntityChangeSignal changeSignal = null, IServiceProvider services = null)
        {
            this.contextFactory = contextFactory;
            this.securityAccessProvider = securityAccessProvider;
            this.bufferOptions = bufferOptions;
            this.logger = logger;
            this.changeSignal = changeSignal;
            this.services = services;
        }

        /// <summary>
        /// Leases a fresh per-operation security context for the duration of a single operation. The same context is
        /// threaded through any private helpers so a whole public operation runs on ONE leased context.
        /// </summary>
        private IContextLease<IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>> LeaseContext()
            => contextFactory.Lease<IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>>();

        public string UniqueName { get; set; }

        public ICollection<User> Users
        {
            get
            {
                using var lease = LeaseContext();
                var securityContext = lease.Context;
                return (from u in securityContext.Users.ToList()
                    select SelectUser(u)).ToList();
            }
        }

        public ICollection<Role> Roles
        {
            get
            {
                using var lease = LeaseContext();
                var securityContext = lease.Context;
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
                return (from r in securityContext.SecurityRoles where r.TenantId == securityContext.CurrentTenantId select r).ToList<Role>();
            }
        }

        public ICollection<Permission> Permissions
        {
            get
            {
                using var lease = LeaseContext();
                var securityContext = lease.Context;
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = false});
                return (from p in securityContext.Permissions where p.TenantId == null || p.TenantId == securityContext.CurrentTenantId select p).ToList<Permission>();
            }
        }

        public IEnumerable<Role> GetRoles(User user)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            using var tmp = securityAccessProvider.CreateForCaller(
                securityContext,
                new TTrustConfig { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = true});
            return (from r in AllRoles(securityContext.Users.First(UserFilter(user))) select r.Role).ToArray();
        }

        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            int? t;
            using (var lease = LeaseContext())
            {
                t = lease.Context.CurrentTenantId;
            }
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
            // Render-path read on a fresh per-operation context (collision-free w.r.t. the shared circuit context).
            // The whole body runs on this single leased instance; GetRawUserQuery gets it explicitly via readCtx.
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            {
                if (t != null)
                {
                    var ti = t.Value;
                    var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
                    using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                        new TTrustConfig { HideGlobals = false, IncludeParentTree = isUser, ShowAllTenants = false });
                    if (isUser)
                    {
                        // Existence-only: IsAuthenticated needs to know the user is reachable in the current tenant
                        // tree, not their resolved roles, so it checks the tree ONCE (the phase1 join) instead of
                        // the two-pass role-resolving GetRawUserQuery — a smaller query with a smaller memory grant.
                        var reachableUserIds = from tu in securityContext.TenantUsers
                                               join j in securityContext.GetUpwardsTenantUserRoles(userLabels, securityContext.CurrentTenantName)
                                                   on tu.TenantUserId equals j.TenantUserId
                                               where j.OutermostLeafTenantId == ti
                                               select tu.UserId;
                        return securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType))
                            .Join(reachableUserIds, UserId, x => x, (u, x) => 1)
                            .Any();
                    }

                    var filteredLabels = (from ul in userLabels
                        where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                        select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                    var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ti);
                    var tenantUsers = appUsers
                        .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                        .Select(n => new UserTenantLevel<TUser>{User=n.TenantUser.User,TenantId = ti, Level=1});

                    return tenantUsers.Select(n => n.User).Any(UserFilter(userLabels, userAuthenticationType));
                }

                return false;
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
            // Render-path read on a fresh per-operation context (collision-free w.r.t. the shared circuit context).
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            {
                var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    new TTrustConfig { HideGlobals = false, IncludeParentTree = isUser, ShowAllTenants = true });
                var t = securityContext.Tenants.FirstOrDefault(n => n.TenantName == forScope)?.TenantId;
                if (t != null)
                {
                    var ti = t.Value;

                    if (isUser)
                    {
                        // Existence-only (see IsAuthenticatedCore): one tree pass instead of GetRawUserQuery's two.
                        var reachableUserIds = from tu in securityContext.TenantUsers
                                               join j in securityContext.GetUpwardsTenantUserRoles(userLabels, forScope)
                                                   on tu.TenantUserId equals j.TenantUserId
                                               where j.OutermostLeafTenantId == ti
                                               select tu.UserId;
                        return securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType))
                            .Join(reachableUserIds, UserId, x => x, (u, x) => 1)
                            .Any();
                    }

                    var filteredLabels = (from ul in userLabels
                        where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                        select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
                    var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == ti);
                    var tenantUsers = appUsers
                        .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                        .Select(n => new UserTenantLevel<TUser> { User = n.TenantUser.User, TenantId = ti, Level = 1 });

                    return tenantUsers.Select(n => n.User).Any(UserFilter(userLabels, userAuthenticationType));
                }

                return false;
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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

            using var lease = LeaseContext();
            var securityContext = lease.Context;
            var isUser = userLabels.All(n => string.IsNullOrEmpty(n) || !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser}));
            IQueryable<UserTenantLevel<TUser>> tenantUsers;
            if (isUser)
            {
                tenantUsers = GetRawUserQuery(out _, userLabels, readCtx: securityContext);
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            // Render-path read on a fresh per-operation context -> collision-free w.r.t. the shared circuit context.
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            {
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                ConfigureTrustConfig(new()
                    { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser }));

            if (isUser)
            {
                var preFiltered = GetRawUserQuery(out var currentTenant, userLabels, readCtx: securityContext);
                // See GetPermissions(labels, forScope, authType): materialize the effective role-ids once so the
                // recursive tree runs once (not 4x via the UNION branches) and the memory grant stays small; the
                // permission lookups then run as light IN-list queries.
                var roleIds = securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(preFiltered, UserId, IdOfUserLevelRecord, (l, r) => new { r.TenantId, r.RoleId })
                    .Where(n => n.TenantId == currentTenant)
                    .Select(n => n.RoleId)
                    .Distinct()
                    .ToArray();

                var localPerms = securityContext.RolePermissions
                    .Where(rp => roleIds.Contains(rp.RoleId))
                    .Select(rp => rp.Permission);
                var globalPerms = securityContext.GlobalToLocalRoles
                    .Where(rj => roleIds.Contains(rj.LocalRoleId))
                    .SelectMany(rj => rj.GlobalRole.RolePermissions)
                    .Select(grp => grp.Permission);
                var parr = localPerms.Union(globalPerms).Distinct().ToArray();
                return parr;
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
        }

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string forScope, string userAuthenticationType)
        {
            // Render-path read on a fresh per-operation context -> collision-free w.r.t. the shared circuit context.
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            {
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                ConfigureTrustConfig(new()
                { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = isUser }));

            if (isUser)
            {
                var preFiltered = GetRawUserQuery(out var currentTenantId, userLabels, forScope, securityContext);
                // Resolve the user's effective role-ids in the current tenant in a SINGLE query and materialize
                // them, so the recursive upwards-role-tree is evaluated once instead of being re-inlined into both
                // UNION branches below. The previously-composed query ran the recursive tree FOUR times (2x per
                // branch) with UNION+DISTINCT on top, which makes the optimizer request a large memory grant; under
                // concurrency (the per-write scope-resolution fan-out) those grants exhaust a small memory pool
                // (LocalDB) and the query stalls on RESOURCE_SEMAPHORE until the command timeout. Splitting it into
                // one tree query + two light IN-list permission lookups keeps every grant tiny.
                var roleIds = securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(preFiltered, UserId, IdOfUserLevelRecord, (l, r) => new { r.TenantId, r.RoleId })
                    .Where(n => n.TenantId == currentTenantId)
                    .Select(n => n.RoleId)
                    .Distinct()
                    .ToArray();

                var localPerms = securityContext.RolePermissions
                    .Where(rp => roleIds.Contains(rp.RoleId))
                    .Select(rp => rp.Permission);
                var globalPerms = securityContext.GlobalToLocalRoles
                    .Where(rj => roleIds.Contains(rj.LocalRoleId))
                    .SelectMany(rj => rj.GlobalRole.RolePermissions)
                    .Select(grp => grp.Permission);
                var parr = localPerms.Union(globalPerms).Distinct().ToArray();
                return parr;
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
        }

        /// <summary>
        /// Bootstrap helper: ensures the requested permissions exist (created as global permissions when missing)
        /// and — when a target global role is configured — grants them to that role. Each distinct name is handled
        /// at most once per process (claim-based dedup), so this stays cheap on the authorization hot-path. The
        /// write goes through a freshly leased per-operation context, so it never collides with concurrent reads on
        /// the circuit/request-scoped context; the entity-write-tracker raises the security change-signal, so the
        /// new grants take effect within the live session.
        /// </summary>
        public void EnsureRequestedPermissions(string[] permissionNames, AutoPermissionsOptions options)
        {
            if (options is not { Enabled: true } || permissionNames == null || permissionNames.Length == 0)
            {
                return;
            }

            // Called off the hot-path by the batching registrar with an already-deduped set; the create/grant is
            // additionally idempotent against the database (existence + already-granted checks below).
            var pending = permissionNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            var grantRoleName = options.GrantToGlobalRole;
            try
            {
                using var lease = LeaseContext();
                var securityContext = lease.Context;
                using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                    ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));

                TGlobalRole grantRole = null;
                if (!string.IsNullOrEmpty(grantRoleName))
                {
                    grantRole = securityContext.GlobalRoles.FirstOrDefault(r => r.RoleName == grantRoleName);
                    if (grantRole == null)
                    {
                        logger.LogWarning(
                            "Auto-permission-registration: configured global role '{role}' was not found; requested permissions are created without a grant.",
                            grantRoleName);
                    }
                }

                bool dirty = false;
                foreach (var name in pending)
                {
                    var permission = securityContext.Permissions.FirstOrDefault(p => p.TenantId == null && p.PermissionName == name);
                    if (permission == null)
                    {
                        permission = Activator.CreateInstance<TPermission>();
                        permission.PermissionName = name;
                        permission.Description = "Auto-registered on first request";
                        permission.TenantId = null;
                        securityContext.Permissions.Add(permission);
                        dirty = true;
                        logger.LogInformation("Auto-registered permission '{permission}'.", name);
                    }

                    if (grantRole != null)
                    {
                        // For an existing permission, skip if the grant is already present. A freshly added permission
                        // has no id yet, so it cannot have a grant; let EF resolve both FKs through the navigations.
                        bool alreadyGranted = permission.PermissionId != 0 &&
                            securityContext.GlobalRolePermissions.Any(g => g.GlobalRoleId == grantRole.GlobalRoleId && g.PermissionId == permission.PermissionId);
                        if (!alreadyGranted)
                        {
                            var grant = Activator.CreateInstance<TGlobalRolePermission>();
                            grant.GlobalRole = grantRole;
                            grant.Permission = permission;
                            securityContext.GlobalRolePermissions.Add(grant);
                            dirty = true;
                        }
                    }
                }

                if (dirty)
                {
                    // Fail-fast, tracking-suppressed and idempotent: never stalls the render hot-path on lock/schema
                    // contention and never storms every circuit with a redundant permission re-resolve. See guard.
                    Shared.Security.AutoPermissionWriteGuard.SaveIdempotent(securityContext as DbContext, logger,
                        options.WriteCommandTimeoutSeconds);
                }
            }
            catch (Exception e)
            {
                // Surface the failure so the batching registrar releases these names and re-enqueues them on a
                // later request (a duplicate-key race is already absorbed as success inside the write-guard, so it
                // never reaches here). Swallowing here would instead mark the batch permanently handled and, with
                // the fail-fast write timeout, leave a contended permission un-registered until a process restart.
                logger.LogWarning(e, "Auto-permission-registration write failed; the batch will be retried on a later request.");
                throw;
            }
        }

        /// <summary>
        /// Gets the names of the global roles the given user effectively holds in the current tenant scope. For a
        /// normal user this is resolved through the tree (<c>GetRawUserQuery</c>) joined to the global-to-local-role
        /// mapping; for a client-app user via the roles' <c>PermittedGlobalRoles</c>. Mirrors the global-role branch
        /// of <see cref="GetPermissions(string[],string)"/>; see <see cref="ISecurityRepository.GetGlobalRoles"/>.
        /// </summary>
        public virtual string[] GetGlobalRoles(string[] userLabels, string userAuthenticationType)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            var isUser = userLabels.All(n => !Regex.IsMatch(n, Global.AppUserKeyPattern));
            using var tmp = securityAccessProvider.CreateForCaller(securityContext,
                ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false, IncludeParentTree = isUser }));
            if (securityContext.CurrentTenantId == null)
            {
                // No current tenant scope -> no tenant-role-derived global roles to resolve. Degrade gracefully
                // (the optimistic fast-path simply does not apply) rather than dereferencing a null tenant id.
                return Array.Empty<string>();
            }

            if (isUser)
            {
                var preFiltered = GetRawUserQuery(out var currentTenant, userLabels, readCtx: securityContext);
                var tmptu = securityContext.Users.Where(UserFilter(userLabels, userAuthenticationType)).Join(
                    preFiltered, UserId, IdOfUserLevelRecord, (l, r) => new { r.TenantId, r.RoleId });
                return (from t in tmptu
                    join rj in securityContext.GlobalToLocalRoles on t.RoleId equals rj.LocalRoleId
                    join gr in securityContext.GlobalRoles on rj.GlobalRoleId equals gr.GlobalRoleId
                    where t.TenantId == currentTenant
                    select gr.RoleName).Distinct().ToArray();
            }

            var filteredLabels = (from ul in userLabels
                where Regex.IsMatch(ul, Global.AppUserKeyPattern)
                select Regex.Match(ul, Global.AppUserKeyPattern).Groups["appUserKey"].Value).ToArray();
            var appUsers = securityContext.ClientAppUsers.Where(n => n.TenantUser.TenantId == securityContext.CurrentTenantId.Value);
            var tenantUsers = appUsers
                .Where(au => filteredLabels.Contains(au.Label, StringComparer.OrdinalIgnoreCase))
                .Select(n => n.TenantUser.User);
            var roles = from tr in tenantUsers.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(securityContext.TenantUsers, UserId, tr => tr.UserId, (tu, tt) => tt)
                join ur in securityContext.TenantUserRoles on tr.TenantUserId equals ur.TenantUserId.Value
                join r in securityContext.SecurityRoles on new { RoleId = ur.RoleId.Value, tr.TenantId } equals new { r.RoleId, r.TenantId }
                select r;
            return roles.SelectMany(n => n.PermittedGlobalRoles.Select(pgr => pgr.GlobalRole.RoleName)).Distinct().ToArray();
        }

        public IEnumerable<Permission> GetPermissions(Role role)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            using var tmp = securityAccessProvider.CreateForCaller(securityContext, ConfigureTrustConfig(new() { ShowAllTenants = false, HideGlobals = false }));
            return securityContext.Tenants.Any(n => n.TenantName == permissionScopeName);
        }

        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType)
        {
            // Resolve eligible scopes on a fresh per-operation context instance instead of a shared circuit-scoped
            // one. This runs synchronously from CurrentTenantId/PermissionPrefix while parallel Blazor lifecycle
            // callbacks may have an operation open on the shared instance -> "a second operation was started on this
            // context instance". A fresh leased instance is correctly scoped yet collision-free (mirrors the
            // navigation builder, f53cae61). The trust elevation below targets the leased instance; the trust lookup
            // itself is served from the access-provider cache, so no query hits the shared context.
            using var lease = LeaseContext();
            var ctx = lease.Context;
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
        }

        /// <summary>
        /// Roots of the lazily-expandable tenant tree (Option B): the topmost tenants the user can access — i.e.
        /// holds at least one permission there (directly or via a role inherited down the hierarchy, incl. a global
        /// role a local role draws permissions from). Pass-through tenants (a role but no permission) are collapsed.
        /// Runs on a fresh per-operation context instance (collision-free w.r.t. the shared circuit context) with the
        /// tenant filter opened, exactly like <see cref="GetEligibleScopes"/>.
        /// </summary>
        public IReadOnlyList<TenantTreeNode> GetRootTenants(string[] userLabels, string userAuthenticationType)
            => ReadDetached(ctx =>
            {
                using var tmp = securityAccessProvider.CreateForCaller(ctx,
                    ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = false }));

                var userTuIds = ctx.Users.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(ctx.TenantUsers, UserId, tu => tu.UserId, (u, tu) => tu.TenantUserId).ToArray();

                var walker = new TenantTreeWalker(ChildLevel(ctx, userTuIds), ParentOf(ctx));

                // Seeds = the user's direct memberships with their directly-assigned roles (+ whether that role
                // yields any permission at that tenant). The walker turns these into the tree roots.
                var seedRows = (
                    from tu in ctx.TenantUsers
                    where userTuIds.Contains(tu.TenantUserId)
                    from tur in ctx.TenantUserRoles.Where(x => x.TenantUserId == tu.TenantUserId)
                    from r in ctx.SecurityRoles.Where(x => x.RoleId == tur.RoleId)
                    from t in ctx.Tenants.Where(x => x.TenantId == tu.TenantId)
                    select new TenantTreeLevelRow
                    {
                        TenantId = t.TenantId,
                        ParentTenantId = t.ParentTenantId,
                        TenantName = t.TenantName,
                        DisplayName = t.DisplayName,
                        RoleId = r.RoleId,
                        HasPermission = ctx.RolePermissions.Any(rp => rp.RoleId == r.RoleId)
                            || ctx.GlobalToLocalRoles.Any(gl => gl.LocalRoleId == r.RoleId
                                    && ctx.GlobalRolePermissions.Any(grp => grp.GlobalRoleId == gl.GlobalRoleId)),
                        Direct = true
                    }).ToList();

                return walker.BuildRoots(seedRows);
            });

        /// <summary>
        /// The accessible child tenants of <paramref name="parentTenantId"/> (nearest accessible descendants,
        /// pass-throughs collapsed), resolved from the roles the user holds at the parent
        /// (<paramref name="carriedRoleIds"/>, carried forward from that node). One lazy level per expand.
        /// </summary>
        public IReadOnlyList<TenantTreeNode> GetChildTenants(string[] userLabels, string userAuthenticationType, int parentTenantId, int[] carriedRoleIds)
            => ReadDetached(ctx =>
            {
                using var tmp = securityAccessProvider.CreateForCaller(ctx,
                    ConfigureTrustConfig(new() { ShowAllTenants = true, HideGlobals = false, IncludeParentTree = false }));

                var userTuIds = ctx.Users.Where(UserFilter(userLabels, userAuthenticationType))
                    .Join(ctx.TenantUsers, UserId, tu => tu.UserId, (u, tu) => tu.TenantUserId).ToArray();

                var walker = new TenantTreeWalker(ChildLevel(ctx, userTuIds), ParentOf(ctx));
                return walker.BuildChildren(parentTenantId, carriedRoleIds ?? Array.Empty<int>());
            });

        /// <summary>
        /// Builds the one-structural-level lookup used by <see cref="TenantTreeWalker"/>: given a parent tenant and
        /// the roles the user holds there, returns each direct structural child paired with each role the user
        /// resolves at it — inherited (a <c>RoleRoles</c> edge from a carried role) or by direct membership — plus a
        /// per-role flag for whether that role yields any permission (local <c>RolePermissions</c>, or a global role
        /// the local role draws from). Non-recursive: anchored on the parent, scaling with the child count.
        /// </summary>
        private Func<int, int[], List<TenantTreeLevelRow>> ChildLevel(
            IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> ctx,
            int[] userTuIds)
            => (parentTenantId, carried) =>
            {
                // "Diskrete Weitergabe": expand the carried roles with the intra-tenant RoleRoles closure at the parent
                // tenant BEFORE matching the single cross-tenant edge down to the children. Without this, a role the
                // user only holds through an in-tenant inheritance edge (e.g. a PermissionSet activation: DirectRole ->
                // Set-Role) would not carry the Set-Role's cross-tenant (downline) reach, so an activated downline role
                // never propagates. See docs/ISSUE-MLM-PermissionSet-CrossTenant-Propagation.md.
                var effectiveCarried = ExpandIntraTenantClosure(ctx, carried, parentTenantId);
                return (
                    from c in ctx.Tenants
                    where c.ParentTenantId == parentTenantId
                    from sc in ctx.SecurityRoles.Where(x => x.TenantId == c.TenantId)
                    where ctx.RoleRoles.Any(rr => rr.PermissiveRoleId == sc.RoleId && rr.PermittedRoleId != null && effectiveCarried.Contains(rr.PermittedRoleId.Value))
                       || ctx.TenantUserRoles.Any(tur => tur.RoleId == sc.RoleId && tur.TenantUserId != null && userTuIds.Contains(tur.TenantUserId.Value))
                    select new TenantTreeLevelRow
                    {
                        TenantId = c.TenantId,
                        ParentTenantId = c.ParentTenantId,
                        TenantName = c.TenantName,
                        DisplayName = c.DisplayName,
                        RoleId = sc.RoleId,
                        HasPermission = ctx.RolePermissions.Any(rp => rp.RoleId == sc.RoleId)
                            || ctx.GlobalToLocalRoles.Any(gl => gl.LocalRoleId == sc.RoleId
                                    && ctx.GlobalRolePermissions.Any(grp => grp.GlobalRoleId == gl.GlobalRoleId)),
                        Direct = ctx.TenantUserRoles.Any(tur => tur.RoleId == sc.RoleId && tur.TenantUserId != null && userTuIds.Contains(tur.TenantUserId.Value))
                    }).ToList();
            };

        private Func<int, int?> ParentOf(
            IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> ctx)
            => tenantId => ctx.Tenants.Where(t => t.TenantId == tenantId).Select(t => t.ParentTenantId).FirstOrDefault();

        /// <summary>
        /// Expands <paramref name="roleIds"/> (roles the user effectively holds in <paramref name="tenantId"/>) with the
        /// intra-tenant <c>RoleRoles</c> closure: for an in-tenant edge ("Permissive is reached when Permitted is held")
        /// every Permissive role whose Permitted role is already in the set is added, transitively. This is the discrete
        /// in-tenant propagation that lets a PermissionSet-activated role (the DirectRole -> Set-Role edge) also carry
        /// the Set-Role's cross-tenant (downline) reach. Only same-tenant edges are followed — cross-tenant reach stays
        /// the tree walker's job (one structural level per edge). Terminates because cyclic role inheritance is
        /// prevented (guarded regardless). See docs/ISSUE-MLM-PermissionSet-CrossTenant-Propagation.md.
        /// </summary>
        private static int[] ExpandIntraTenantClosure(
            IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> ctx,
            int[] roleIds, int tenantId)
        {
            var closure = new HashSet<int>(roleIds ?? Array.Empty<int>());
            if (closure.Count == 0)
            {
                return Array.Empty<int>();
            }

            var grown = true;
            var guard = 0;
            while (grown && guard++ < 4096)
            {
                var current = closure.ToArray();
                var next = (from rr in ctx.RoleRoles
                        where rr.PermittedRoleId != null && rr.PermissiveRoleId != null
                              && current.Contains(rr.PermittedRoleId.Value)
                        join s in ctx.SecurityRoles on rr.PermissiveRoleId.Value equals s.RoleId
                        where s.TenantId == tenantId
                        select s.RoleId).ToArray();
                grown = false;
                foreach (var id in next)
                {
                    grown |= closure.Add(id);
                }
            }

            return closure.ToArray();
        }

        private sealed class TenantTreeLevelRow
        {
            public int TenantId { get; set; }
            public int? ParentTenantId { get; set; }
            public string TenantName { get; set; }
            public string DisplayName { get; set; }
            public int RoleId { get; set; }
            public bool HasPermission { get; set; }
            public bool Direct { get; set; }
        }

        private sealed class TenantTreeNodeAgg
        {
            public int TenantId { get; set; }
            public int? ParentTenantId { get; set; }
            public string TenantName { get; set; }
            public string DisplayName { get; set; }
            public int[] RoleIds { get; set; }
            public bool Accessible { get; set; }
            public bool Direct { get; set; }
        }

        /// <summary>
        /// DB-agnostic Option-B tree assembly over two lookups: <c>childLevel(parentId, carriedRoleIds)</c> yields
        /// the direct structural children (one (child, role) row each, with a per-role has-permission flag), and
        /// <c>parentOf(tenantId)</c> yields a tenant's structural parent. All hierarchy / pass-through-collapse logic
        /// lives here; the caller supplies only the two DB lookups. Cost scales with the visited span (children +
        /// pass-through depth), never with the whole tree.
        /// </summary>
        private sealed class TenantTreeWalker
        {
            private readonly Func<int, int[], List<TenantTreeLevelRow>> childLevel;
            private readonly Func<int, int?> parentOf;
            private readonly Dictionary<int, int?> parentCache = new();

            public TenantTreeWalker(Func<int, int[], List<TenantTreeLevelRow>> childLevel, Func<int, int?> parentOf)
            {
                this.childLevel = childLevel;
                this.parentOf = parentOf;
            }

            public IReadOnlyList<TenantTreeNode> BuildRoots(List<TenantTreeLevelRow> seedRows)
            {
                var seeds = Aggregate(seedRows);
                var seedIds = seeds.Select(s => s.TenantId).ToHashSet();
                foreach (var s in seeds)
                {
                    parentCache[s.TenantId] = s.ParentTenantId;
                }

                // Structural roots = seed tenants with no seed among their structural ancestors. Roles only inherit
                // downward, so the topmost accessible node of any chain sits at or below such a seed; walking down
                // from these (collapsing pass-throughs) yields the display roots without cross-nesting.
                var candidates = new List<TenantTreeNodeAgg>();
                var visited = new HashSet<int>();
                foreach (var s in seeds.Where(s => !HasSeedAncestor(s, seedIds)))
                {
                    if (!visited.Add(s.TenantId))
                    {
                        continue;
                    }

                    if (s.Accessible)
                    {
                        candidates.Add(s);
                    }
                    else
                    {
                        CollectFrontier(s.TenantId, s.RoleIds, candidates, visited);
                    }
                }

                return candidates.Select(ToNode).ToList();
            }

            public IReadOnlyList<TenantTreeNode> BuildChildren(int parentTenantId, int[] carriedRoleIds)
            {
                var frontier = new List<TenantTreeNodeAgg>();
                var visited = new HashSet<int> { parentTenantId };
                CollectFrontier(parentTenantId, carriedRoleIds, frontier, visited);
                return frontier.Select(ToNode).ToList();
            }

            // Descend from a node, emitting the first accessible tenant on each downward path and skipping
            // pass-through tenants (a role but no permission).
            private void CollectFrontier(int parentTenantId, int[] carried, List<TenantTreeNodeAgg> result, HashSet<int> visited)
            {
                foreach (var child in Aggregate(childLevel(parentTenantId, carried)))
                {
                    if (!visited.Add(child.TenantId))
                    {
                        continue;
                    }

                    if (child.Accessible)
                    {
                        result.Add(child);
                    }
                    else
                    {
                        CollectFrontier(child.TenantId, child.RoleIds, result, visited);
                    }
                }
            }

            // Whether any accessible tenant exists strictly below the node (short-circuits at the first hit).
            private bool HasAccessibleDescendant(int tenantId, int[] carried)
            {
                var visited = new HashSet<int> { tenantId };
                return Descend(tenantId, carried);

                bool Descend(int t, int[] c)
                {
                    foreach (var child in Aggregate(childLevel(t, c)))
                    {
                        if (!visited.Add(child.TenantId))
                        {
                            continue;
                        }

                        if (child.Accessible || Descend(child.TenantId, child.RoleIds))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            private bool HasSeedAncestor(TenantTreeNodeAgg node, HashSet<int> seedIds)
            {
                var p = node.ParentTenantId;
                var guard = 0;
                while (p != null && guard++ < 4096)
                {
                    if (seedIds.Contains(p.Value))
                    {
                        return true;
                    }

                    p = ParentOfCached(p.Value);
                }

                return false;
            }

            private int? ParentOfCached(int tenantId)
            {
                if (!parentCache.TryGetValue(tenantId, out var p))
                {
                    p = parentOf(tenantId);
                    parentCache[tenantId] = p;
                }

                return p;
            }

            private static List<TenantTreeNodeAgg> Aggregate(List<TenantTreeLevelRow> rows)
                => rows.GroupBy(r => r.TenantId).Select(g => new TenantTreeNodeAgg
                {
                    TenantId = g.Key,
                    ParentTenantId = g.First().ParentTenantId,
                    TenantName = g.First().TenantName,
                    DisplayName = g.First().DisplayName,
                    RoleIds = g.Select(x => x.RoleId).Distinct().ToArray(),
                    Accessible = g.Any(x => x.HasPermission),
                    Direct = g.Any(x => x.Direct)
                }).ToList();

            private TenantTreeNode ToNode(TenantTreeNodeAgg n) => new TenantTreeNode
            {
                TenantId = n.TenantId,
                ParentTenantId = n.ParentTenantId,
                TenantName = n.TenantName,
                DisplayName = n.DisplayName,
                AccessMode = n.Direct ? ScopeAccessMode.Direct : ScopeAccessMode.Inherited,
                CarriedRoleIds = n.RoleIds,
                HasAccessibleChildren = HasAccessibleDescendant(n.TenantId, n.RoleIds)
            };
        }

        /// <summary>
        /// Runs a read on a dedicated, fresh per-operation context instance (collision-free w.r.t. the shared circuit
        /// context) and disposes it afterwards. The result MUST be fully materialized inside <paramref name="read"/>
        /// (do not return a lazy IQueryable).
        /// </summary>
        private TResult ReadDetached<TResult>(Func<IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig>, TResult> read)
        {
            using var lease = LeaseContext();
            return read(lease.Context);
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            var timezone = GetTimeZone(permissionScopeName, securityContext);
            return new TimeZoneHelper(timezone);
        }

        public string Decrypt(string encryptedValue, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.DecryptForScope(encryptedValue, permissionScopeName, initializationVector, salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.GetDecryptStreamForScope(baseStream, permissionScopeName, initializationVector, salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.GetDecryptStreamForScope(baseStream, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public string Encrypt(string value, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.EncryptForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.EncryptForScope(value, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.EncryptForScope(value, permissionScopeName, out initializationVector, out salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector,
            out byte[] salt)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.GetEncryptStreamForScope(baseStream, permissionScopeName, out initializationVector, out salt, n => ConfigureTrustConfig(n));
        }

        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            return securityContext.GetEncryptStreamForScope(baseStream, permissionScopeName, n => ConfigureTrustConfig(n));
        }

        public string EncryptJsonObject(object value, string permissionScopeName)
        {
            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            ExternalServiceConnection tmp;
            string tenantName;
            using (var lease = LeaseContext())
            {
                tmp = GetExternalOAuthService(name, out _, out _, out tenantName, true, lease.Context).Copy();
            }
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            var tmp = GetExternalOAuthService(oAuthState.ConnectionName, out var serviceId, out var tenantId, out _, true, securityContext);
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

            using var lease = LeaseContext();
            var securityContext = lease.Context;
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
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            var tmp = GetExternalOAuthService(connectionName, out var serviceId, out var tenantId, out var tenantName, true, securityContext);
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
            // Per-operation: this read runs on one leased context (disposed via 'using'). The returned
            // 'updateToken' delegate is invoked by the caller later and persists on its OWN fresh lease
            // (see PersistToken) — it captures only ids/values, never this context or a tracked entity.
            using var lease = LeaseContext();
            var securityContext = lease.Context;
            using var tmpSecurity = securityAccessProvider.CreateForCaller(securityContext,
                new TTrustConfig { HideGlobals = false, IncludeParentTree = true, ShowAllTenants = false });
            var svc = GetExternalOAuthService(connectionName, out var serviceId, out var tenantId, out var tenantName, false, securityContext).Copy();
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
                var login = GetExternalOAuthServiceLogin(serviceId.Value,false, securityContext);
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
                        updateToken = newToken => PersistToken(serviceId.Value, tenantId, targetTenant, newToken);
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
                    updateToken = newToken => PersistToken(serviceId.Value, tenantId, targetTenant, newToken);

                    return null;
                }
            }

            throw new InvalidOperationException($"No appropriate Token found for {connectionName}");
        }

        /// <summary>
        /// Persists (creates or updates) the OAuth tenant-login token on its OWN fresh per-operation context.
        /// Called by the deferred <c>updateToken</c> delegate returned from <see cref="GetBufferedToken"/>; it
        /// captures only ids/values (no tracked entity or shared context), re-resolves the login by service
        /// (across the tenant tree, same trust as the read) and saves atomically.
        /// </summary>
        private void PersistToken(int serviceId, int? tenantId, string targetTenant, TranslatedTokenResponse newToken)
        {
            var encToken = new TranslatedTokenResponse
            {
                Scope = newToken.Scope,
                AccessToken = Encrypt(newToken.AccessToken, targetTenant),
                ExpiresAt = newToken.ExpiresAt,
                RefreshToken = Encrypt(newToken.RefreshToken, targetTenant),
                TokenType = newToken.TokenType
            };

            using var lease = LeaseContext();
            var securityContext = lease.Context;
            using var tmpSecurity = securityAccessProvider.CreateForCaller(securityContext,
                new TTrustConfig { HideGlobals = false, IncludeParentTree = true, ShowAllTenants = false });
            var login = GetExternalOAuthServiceLogin(serviceId, false, securityContext);
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
                    OAuthServiceId = serviceId,
                    Token = JsonHelper.ToJson(encToken, SerializationTypingMode.StaticTyping)
                };

                securityContext.ExternalOAuthServiceTenantLogins.Add(login);
            }

            securityContext.SaveChanges();
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

        protected virtual ExternalServiceConnection GetExternalOAuthService(string name, out int? externalOAuthServiceId, out int? tenantId, out string? tenantName, bool useFullAccess, IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext)
        {
            if (securityContext.FilterAvailable && !securityContext.ShowAllTenants && ServiceBuffered(name, out var bufferInfo, securityContext))
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
                    return TryRegisterService(name, pi, securityContext);
                }
                finally
                {
                    tmp?.Dispose();
                }
            }

            throw new InvalidOperationException("Invalid DB-Access mode");
        }

        protected virtual TExternalOAuthServiceTenantLogin GetExternalOAuthServiceLogin(int oauthServiceId, bool useFullAccess, IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext)
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

        private TimeZoneInfo GetTimeZone(string permissionScopeName, IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext)
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
            // Always runs on the caller's leased per-operation context (collision-free); callers pass it via readCtx.
            var sc = readCtx;
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

            // Je Benutzer die naechstgelegenen Zeilen (kleinstes ParentLevel) - in EINEM Baum-Durchlauf
            // statt in zweien. Wie das ausgedrueckt wird, entscheidet der Provider; hier steht bewusst
            // kein SQL mehr, sonst laesst sich diese Klasse auf keiner zweiten Datenbank betreiben.
            //
            // Das Ergebnis bleibt eine offene Abfrage: der Join unten gehoert in dieselbe
            // Datenbankrunde, und genau das war der Sinn der Zusammenfassung auf einen Durchlauf.
            var closest = sc.GetClosestUpwardsTenantUserRoles(userLabels, forTenant, currentTenant);

            return from t in closest
                join tn in sc.TenantUsers on t.TenantUserId equals tn.TenantUserId
                select new UserTenantLevel<TUser>
                {
                    User = tn.User, Level = t.ParentLevel, TenantId = t.OutermostLeafTenantId,
                    RoleId = t.OutermostRoleId
                };
        }

        private ExternalServiceConnection TryRegisterService(string uniqueName, TExternalOAuthService serviceData, IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext)
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

        private bool ServiceBuffered(string name, out ExternalOAuthServiceBufferInfo/*<TTenant, TWebPlugin, TWebPluginGenericParameter>*/ bufferInfo, IHierarchySecurityContext<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TNavigationMenu, TTenantNavigation, TQuery, TQueryParameter, TTenantQuery, TWidget, TWidgetParam, TWidgetLocalization, TUserWidget, TUserProperty, TAssetTemplate, TAssetTemplatePath, TAssetTemplateGrant, TAssetTemplateFeature, TSharedAsset, TSharedAssetUserFilter, TSharedAssetTenantFilter, TClientAppTemplate, TAppPermission, TAppPermissionSet, TClientAppTemplatePermission, TClientApp, TClientAppPermission, TClientAppUser, TWebPlugin, TWebPluginConstant, TWebPluginGenericParameter, TSequence, TTenantSetting, TTenantFeatureActivation, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin, TTrustConfig> securityContext)
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
