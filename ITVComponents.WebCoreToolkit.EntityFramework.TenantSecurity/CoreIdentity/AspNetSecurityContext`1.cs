using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthenticationClaimMapping =
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.AuthenticationClaimMapping;
using AuthenticationType = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.AuthenticationType;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.DashboardWidget;
using DiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.DiagnosticsQuery;
using DiagnosticsQueryParameter =
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.DiagnosticsQueryParameter;
using GlobalSetting = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.GlobalSetting;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.Role;
using RolePermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.RolePermission;
using SystemEvent = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.SystemEvent;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;
using TenantDiagnosticsQuery =
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.TenantDiagnosticsQuery;
using TenantNavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.TenantNavigationMenu;
using TutorialStream = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.TutorialStream;
using AppPermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.AppPermission;
using AppPermissionSet = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.AppPermissionSet;
using ClientAppTemplatePermission =
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.ClientAppTemplatePermission;
using ClientAppTemplate = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.ClientAppTemplate;
using ClientApp = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.ClientApp;
using ClientAppPermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.ClientAppPermission;
using ClientAppUser = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models.ClientAppUser;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity
{
    [ExplicitlyExpose, DenyForeignKeySelection]
    public class AspNetSecurityContext<TImpl> : IdentityDbContext<User>, IForeignKeyProvider,
        ISecurityContext<Tenant, string, User, Role, Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole,
            NavigationMenu, TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery,
            DashboardWidget, DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate,
            AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter,
            SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission,
            ClientApp, ClientAppPermission, ClientAppUser, FlatWebPlugin, FlatWebPluginConstant,
            FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin,
            BaseTenantContextSecurityTrustConfig>, IAllTenantsReader
        where TImpl : AspNetSecurityContext<TImpl>
    {
        protected readonly DbContextModelBuilderOptions<TImpl> modelBuilderOptions;
        private readonly ILogger<TImpl> logger;
        private readonly IPermissionScope tenantProvider;
        private readonly bool useFilters = false;
        private readonly IContextUserProvider userProvider;
        private Dictionary<string, bool> componentSpecialTrusts;
        private bool hideDisabledUsers = true;
        private bool hideGlobals = false;
        private bool showAllTenants = false;
        private int? currentTenantId;
        private string bufferedTenantName;
        private bool currentTenantIdResolved;
        private bool resolvingCurrentTenantId;

        public AspNetSecurityContext(DbContextModelBuilderOptions<TImpl> modelBuilderOptions,
            DbContextOptions<TImpl> options) : base(options)
        {
            this.modelBuilderOptions = modelBuilderOptions;
        }

        public AspNetSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<TImpl> logger, IOptions<DbContextModelBuilderOptions<TImpl>> modelBuilderOptions,
            DbContextOptions<TImpl> options) : base(options)
        {
            this.logger = logger;
            this.tenantProvider = tenantProvider;
            this.userProvider = userProvider;
            useFilters = true;
            this.modelBuilderOptions = modelBuilderOptions.Value;
            /*HideGlobals = true;
            HideGlobals = false;
            ShowAllTenants = false;
            ShowAllTenants = true;*/
            try
            {
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentTenantForFiltering);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => ShowAllTenants);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => FilterAvailable);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => HideGlobals);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => HideDisabledUsers);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentUserName);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentUserId);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentUserMail);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentTenantIdForFiltering);
                //logger.LogDebug($@"SecurityContext initialized. useFilters={useFilters}, CurrentTenant: {tenantProvider?.PermissionPrefix}, ShowAllTenants: {showAllTenants}, HideGlobals: {hideGlobals}");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to configure expression-properties for security context {ContextType}.", typeof(TImpl).FullName);
            }
        }

        /// <summary>
        ///     Gets the user that currently uses this Context
        /// </summary>
        protected IPrincipal Me => userProvider?.User;

        /// <summary>
        ///     Gets a value indicating whetherthe context is configured to work with query-filters
        /// </summary>
        protected bool UseFilters => useFilters;

        public string CurrentTenant
        {
            get
            {
                string retVal = null;
                retVal = tenantProvider.PermissionPrefix?.ToLower();
                return retVal;
            }
        }

        [ExpressionPropertyRedirect("CurrentTenant")]
        private string CurrentTenantForFiltering
        {
            get
            {
                string retVal = null;
                if (!showAllTenants)
                {
                    retVal = tenantProvider.PermissionPrefix?.ToLower();
                }

                return retVal;
            }
        }

        /// <summary>
        ///     Gets the Id of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
        [ExpressionPropertyRedirect("CurrentTenantId")]
        private int? CurrentTenantIdForFiltering
        {
            get
            {
                if (tenantProvider == null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(CurrentTenantForFiltering))
                {
                    return null;
                }

                return CurrentTenantId;
            }
        }

        public string CurrentTenantName => CurrentTenant;

        public int? CurrentTenantId
        {
            get
            {
                if (tenantProvider == null)
                {
                    return null;
                }

                var current = CurrentTenant;
                if (string.IsNullOrEmpty(current))
                {
                    return null;
                }

                // Re-entrancy guard: the Tenants lookup below itself carries the global filter
                // which evaluates CurrentTenantIdForFiltering → CurrentTenantId. Returning the
                // current (possibly null) buffered value avoids a "second operation on the same
                // context" crash and the filter is bypassed at most once during resolution.
                if (resolvingCurrentTenantId)
                {
                    return currentTenantId;
                }

                // Memoize the resolved value (including null) so subsequent calls never re-query.
                if (currentTenantIdResolved && bufferedTenantName == current)
                {
                    return currentTenantId;
                }

                resolvingCurrentTenantId = true;
                try
                {
                    bufferedTenantName = current;
                    currentTenantId = Tenants
                        .FirstOrDefault(n => n.TenantName.ToLower() == current)?.TenantId;
                    currentTenantIdResolved = true;
                    return currentTenantId;
                }
                finally
                {
                    resolvingCurrentTenantId = false;
                }
            }
        }

        public DbSet<FlatWebPluginGenericParameter> GenericPluginParams { get; set; }

        public int SequenceNextVal(string sequenceName)
        {
            var mth = modelBuilderOptions.GetMethod<Func<DbContext, string, int, int>>("SequenceNextVal");
            if (mth == null)
            {
                throw new InvalidOperationException("SequenceNextVal was not implemented for this Database-Type");
            }

            if (CurrentTenantId != null)
            {
                return mth(this, sequenceName, CurrentTenantId.Value);
            }

            return -1;
        }

        public DbSet<FlatSequence> Sequences { get; set; }

        public DbSet<FlatTenantFeatureActivation> TenantFeatureActivations { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View",
            "DiagnosticsQueries.View", "DiagnosticsQueries.Write", "Tenants.SelectFK")]
        public DbSet<Tenant> Tenants { get; set; }

        /// <summary>
        /// Liefert alle Tenants (Id + Name) tenant-agnostisch fuer Hintergrund-Dienste (z.B. den
        /// Workflow-Background-Worker). Die Tenant-Tabelle traegt keinen Query-Filter, daher werden alle
        /// Zeilen geliefert - ohne ShowAllTenants-Escalation und ohne Per-Tenant-Permission.
        /// </summary>
        public IReadOnlyList<TenantIdentity> ReadAllTenants()
            => Tenants.AsNoTracking().Select(t => new TenantIdentity(t.TenantId, t.TenantName)).ToList();

        public DbSet<FlatTenantSetting> TenantSettings { get; set; }

        public DbSet<FlatWebPluginConstant> WebPluginConstants { get; set; }

        public DbSet<FlatWebPlugin> WebPlugins { get; set; }

        public DbSet<AuthenticationClaimMapping> AuthenticationClaimMappings { get; set; }


        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public DbSet<AuthenticationType> AuthenticationTypes { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "DbResources.View", "DbResources.Write")]
        public DbSet<Culture> Cultures { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View")]
        public DbSet<Feature> Features { get; set; }

        /// <summary>
        ///     Indicates whether there is a current http context
        /// </summary>
        [ExpressionPropertyRedirect("FilterAvailable")]
        public bool FilterAvailable =>
            userProvider?.User != null && (userProvider.User.Identities.Any(i => i.IsAuthenticated));

        public DbSet<GlobalSetting> GlobalSettings { get; set; }

        /// <inheritdoc/>
        public DbSet<AssetConsumer> AssetConsumers { get; set; }

        /// <inheritdoc/>
        public DbSet<AssetConsumerArgument> AssetConsumerArguments { get; set; }

        public DbSet<HealthScript> HealthScripts { get; set; }

        /// <summary>
        ///     When tenant filtering is used, this hides tenant-relevant records that are NOT bound to a specific tenant
        /// </summary>
        [ExpressionPropertyRedirect("HideGlobals")]
        public bool HideGlobals
        {
            get => hideGlobals;
            set => hideGlobals = value;
            /*if (value != hideGlobals)
                {
                    var tmp = hideGlobals;
                    hideGlobals = value;
                    if (!value && FilterAvailable &&
                        !userProvider.Services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin }))
                    {
                        hideGlobals = tmp;
                    }
                }*/
        }

        public DbSet<LocalizationCulture> LocalizationCultures { get; set; }

        public DbSet<LocalizationString> LocalizationCultureStrings { get; set; }
        public DbSet<Shared.Models.Localization> Localizations { get; set; }

        /// <summary>
        ///     Indicates whether to switch off tenant filtering
        /// </summary>
        [ExpressionPropertyRedirect("ShowAllTenants")]
        public bool ShowAllTenants
        {
            get => showAllTenants;
            set
            {
                if (value != showAllTenants)
                {
                    var tmp = showAllTenants;
                    showAllTenants = true;
                    if (value && FilterAvailable &&
                        !userProvider.Services.VerifyUserPermissions(new string[] { ToolkitPermission.Sysadmin }))
                    {
                        showAllTenants = tmp;
                    }
                    else
                    {
                        showAllTenants = value;
                    }
                }
            }
        }

        public DbSet<SystemEvent> SystemLog { get; set; }
        public DbSet<TemplateModuleConfiguratorParameter> TemplateModuleConfiguratorParameters { get; set; }
        public DbSet<TemplateModuleConfigurator> TemplateModuleConfigurators { get; set; }

        public DbSet<TemplateModule> TemplateModules { get; set; }
        public DbSet<TemplateModuleScript> TemplateModuleScripts { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public DbSet<TenantTemplate> TenantTemplates { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public DbSet<TenantType> TenantTypes { get; set; }
        public DbSet<ServerCookie> ServerCookies { get; set; }

        public DbSet<TrustedFullAccessComponent> TrustedFullAccessComponents { get; set; }

        public DbSet<VideoTutorial> Tutorials { get; set; }

        public DbSet<TutorialStream> TutorialStreams { get; set; }

        /// <summary>
        ///     Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the
        ///     db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        public IEnumerable GetForeignKeyFilterQuery(string tableName, out Type keyType)
        {
            if (tableName == "TenantSelectionFk")
            {
                keyType = typeof(string);
                return (from t in Tenants
                        orderby t.DisplayName
                        select new ForeignKeyData<string>
                            { Key = t.TenantName, Label = t.DisplayName, FullRecord = t.ToDictionary(true) }).ToList()
                    .Where(n => userProvider.Services.VerifyUserPermissions(new[] { n.Key }));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (userProvider?.User != null)
                {
                    var ret = (from t in Widgets.ToArray()
                        where userProvider.Services.VerifyUserPermissions(new[]
                            { t.DiagnosticsQuery.Permission.PermissionName })
                        orderby t.DisplayName
                        select new ForeignKeyData<int>
                        {
                            Key = t.DashboardWidgetId,
                            Label = t.DisplayName,
                            FullRecord = t.ToDictionary(true)
                        });
                    keyType = typeof(int);
                    return ret;
                }
            }

            /*if (tableName == "Permissions")
            {
                return from t in Permissions orderby t.PermissionName select new ForeignKeyData<int> {Key = t.PermissionId, Label = t.PermissionName};
            }*/

                keyType = typeof(string);
            return null;
        }

        /// <summary>
        ///     Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the
        ///     db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <param name="postedFilter">a filter that was posted when a Foreignkey was queried with POST</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        public IEnumerable GetForeignKeyFilterQuery(string tableName, Dictionary<string, object> postedFilter, out Type keyType)
        {
            var hasPreFilter = postedFilter.TryGetValue("parsedfilter", out var clientQuery);
            FilterBase clientFilter = null;
            if (hasPreFilter && clientQuery is FilterBase fiba)
            {
                clientFilter = fiba;
            }

            if (tableName == "TenantSelectionFk")
            {
                IQueryable<Tenant> ts = Tenants;
                if (clientFilter != null)
                {
                    ts = ts.Where(ExpressionBuilder.BuildExpression<Tenant>(clientFilter, c =>
                    {
                        if (c == "Label")
                        {
                            return new[] { "TenantName", "DisplayName" };
                        }

                        return null;
                    }));
                }

                keyType = typeof(string);
                return (from t in ts
                        orderby t.DisplayName
                        select new ForeignKeyData<string>
                            { Key = t.TenantName, Label = t.DisplayName, FullRecord = t.ToDictionary(true) }).ToList()
                    .Where(n => userProvider.Services.VerifyUserPermissions(new[] { n.Key }));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (userProvider?.User != null)
                {
                    IQueryable<DashboardWidget> wigs = Widgets;
                    if (clientFilter != null)
                    {
                        wigs = wigs.Where(ExpressionBuilder.BuildExpression<DashboardWidget>(clientFilter,
                            c =>
                            {
                                if (c == "Label")
                                {
                                    return new[] { "SystemName", "DisplayName" };
                                }

                                return null;
                            }));
                    }

                    var ret = (from t in wigs.ToArray()
                        where userProvider.Services.VerifyUserPermissions(new[]
                            { t.DiagnosticsQuery.Permission.PermissionName })
                        orderby t.DisplayName
                        select new ForeignKeyData<int>
                        {
                            Key = t.DashboardWidgetId,
                            Label = t.DisplayName,
                            FullRecord = t.ToDictionary(true)
                        });

                    keyType = typeof(int);
                    return ret;
                }
            }

            /*if (tableName == "Permissions")
            {
                return from t in Permissions orderby t.PermissionName select new ForeignKeyData<int> {Key = t.PermissionId, Label = t.PermissionName};
            }*/

            keyType = typeof(string);
            return null;
        }

        public IEnumerable GetForeignKeyResolveQuery(string tableName, object id, out Type keyType)
        {
            if (tableName == "TenantSelectionFk")
            {
                int tid = Convert.ToInt32(id);
                keyType = typeof(string);
                return (from t in Tenants
                        where t.TenantId == tid
                        select new ForeignKeyData<string>
                            { Key = t.TenantName, Label = t.DisplayName, FullRecord = t.ToDictionary(true) }).ToList()
                    .Where(n => userProvider.Services.VerifyUserPermissions(new[] { n.Key }));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (userProvider?.User != null)
                {
                    var ret = (from t in Widgets.ToArray()
                        where userProvider.Services.VerifyUserPermissions(new[]
                                  { t.DiagnosticsQuery.Permission.PermissionName })
                              && t.DashboardWidgetId == Convert.ToInt32(id)
                        orderby t.DisplayName
                        select new ForeignKeyData<int>
                        {
                            Key = t.DashboardWidgetId,
                            Label = t.DisplayName,
                            FullRecord = t.ToDictionary(true)
                        });
                    keyType = typeof(int);
                    return ret;
                }
            }

            keyType = typeof(string);
            return null;
        }

        public DbSet<AppPermission> AppPermissions { get; set; }
        public DbSet<AppPermissionSet> AppPermissionSets { get; set; }
        public DbSet<AssetTemplateFeature> AssetTemplateFeatures { get; set; }
        public DbSet<AssetTemplateGrant> AssetTemplateGrants { get; set; }
        public DbSet<AssetTemplatePath> AssetTemplatePathFilters { get; set; }
        public DbSet<AssetTemplate> AssetTemplates { get; set; }
        public DbSet<ClientAppPermission> ClientAppPermissions { get; set; }
        public DbSet<ClientApp> ClientApps { get; set; }
        public DbSet<ClientAppTemplatePermission> ClientAppTemplatePermissions { get; set; }
        public DbSet<ClientAppTemplate> ClientAppTemplates { get; set; }
        public DbSet<ClientAppUser> ClientAppUsers { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "DashboardWidgets.Write", "DashboardWidgets.View")]
        public DbSet<DiagnosticsQuery> DiagnosticsQueries { get; set; }

        public DbSet<DiagnosticsQueryParameter> DiagnosticsQueryParameters { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether to select disabled users
        /// </summary>
        [ExpressionPropertyRedirect("HideDisabledUsers")]
        public bool HideDisabledUsers
        {
            get => hideDisabledUsers;
            set
            {
                if (value != hideDisabledUsers)
                {
                    if (FilterAvailable &&
                        userProvider.Services.VerifyUserPermissions(new string[]
                            { ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin }))
                    {
                        hideDisabledUsers = value;
                    }
                    else
                    {
                        hideDisabledUsers = true;
                    }
                }
            }
        }

        public DbSet<NavigationMenu> Navigation { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View",
            "DiagnosticsQueries.View", "DiagnosticsQueries.Write", "Permissions.SelectFK")]
        public DbSet<Permission> Permissions { get; set; }

        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<GlobalRole> GlobalRoles { get; set; }
        public DbSet<GlobalRolePermission> GlobalRolePermissions { get; set; }
        public DbSet<GRoleLRole> GlobalToLocalRoles { get; set; }

        public DbSet<RoleRole> RoleRoles { get; set; }

        public DbSet<Role> SecurityRoles { get; set; }
        public DbSet<SharedAsset> SharedAssets { get; set; }
        public DbSet<SharedAssetTenantFilter> SharedAssetTenantFilters { get; set; }
        public DbSet<SharedAssetUserFilter> SharedAssetUserFilters { get; set; }

        public DbSet<TenantDiagnosticsQuery> TenantDiagnosticsQueries { get; set; }

        public DbSet<TenantNavigationMenu> TenantNavigation { get; set; }

        public DbSet<UserRole> TenantUserRoles { get; set; }

        public DbSet<TenantUser> TenantUsers { get; set; }

        public DbSet<CustomUserProperty> UserProperties { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public override DbSet<User> Users { get; set; }

        public DbSet<UserWidget> UserWidgets { get; set; }

        public DbSet<DashboardWidgetLocalization> WidgetLocales { get; set; }

        public DbSet<DashboardParam> WidgetParams { get; set; }

        public DbSet<DashboardWidget> Widgets { get; set; }

        public DbSet<FlatExternalOAuthService> ExternalOAuthServices { get; set; }

        public DbSet<FlatExternalOAuthServiceState> ExternalOAuthServiceStates { get; set; }

        public DbSet<FlatExternalOAuthServiceTenantLogin> ExternalOAuthServiceTenantLogins { get; set; }

        IDictionary<string, bool> ITrustfulComponent<BaseTenantContextSecurityTrustConfig>.ComponentSpecialTrusts => componentSpecialTrusts;

        void ITrustfulComponent<BaseTenantContextSecurityTrustConfig>.ApplyTrust(
            BaseTenantContextSecurityTrustConfig desiredTrust)
        {
            showAllTenants = desiredTrust.ShowAllTenants;
            hideGlobals = desiredTrust.HideGlobals;
            componentSpecialTrusts = desiredTrust.SpecialFilterSettings == null
                ? null
                : new Dictionary<string, bool>(desiredTrust.SpecialFilterSettings);
        }

        BaseTenantContextSecurityTrustConfig ITrustfulComponent<BaseTenantContextSecurityTrustConfig>.GetReverseTrust(
            BaseTenantContextSecurityTrustConfig desiredTrust)
        {
            return new BaseTenantContextSecurityTrustConfig
            {
                HideGlobals = hideGlobals,
                ShowAllTenants = showAllTenants,
                SpecialFilterSettings = componentSpecialTrusts == null
                    ? null
                    : new Dictionary<string, bool>(componentSpecialTrusts)
            };
        }

        Stack<IFullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>>
            ITrustfulComponent<BaseTenantContextSecurityTrustConfig>.securityStateStack { get; } =
            new Stack<IFullSecurityAccessHelper<BaseTenantContextSecurityTrustConfig>>();

        [ExpressionPropertyRedirect("CurrentUserName")]
        public string CurrentUserName => userProvider.User?.Identity?.Name;

        /// <summary>
        ///     Gets the claim-type used to read the current user's unique id. Override to use a custom claim.
        /// </summary>
        protected virtual string UserIdClaimType => System.Security.Claims.ClaimTypes.NameIdentifier;

        /// <summary>
        ///     Gets the claim-type used to read the current user's e-mail. Override to use a custom claim.
        /// </summary>
        protected virtual string UserMailClaimType => System.Security.Claims.ClaimTypes.Email;

        /// <summary>
        ///     Gets the unique id of the current user. Consumed by the Onboarding global filters (replacer "UserId").
        /// </summary>
        [ExpressionPropertyRedirect("UserId")]
        protected virtual string CurrentUserId => (Me?.Identity as ClaimsIdentity)?.FindFirst(UserIdClaimType)?.Value;

        /// <summary>
        ///     Gets the e-mail of the current user. Consumed by the Onboarding global filters (replacer "UserMail").
        /// </summary>
        [ExpressionPropertyRedirect("UserMail")]
        protected virtual string CurrentUserMail => (Me?.Identity as ClaimsIdentity)?.FindFirst(UserMailClaimType)?.Value;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.TableNamesFromProperties(this);

            /*modelBuilder.Entity<Role>().HasMany(n => n.RolePermissions).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<Role>().HasMany(n => n.UserRoles).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<TenantUser>(b => b.Property(n => n.Enabled).HasDefaultValue(true));*/
            modelBuilder.Entity<Role>().HasMany(n => n.RolePermissions).WithOne(p => p.Role)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<Role>().HasMany(n => n.PermittedRoles).WithOne(pr => pr.PermissiveRole)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<Role>().HasMany(n => n.PermissiveRoles).WithOne(pr => pr.PermittedRole)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<Role>().HasMany(n => n.UserRoles).WithOne(p => p.Role)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<TenantUser>(b => b.Property(n => n.Enabled).HasDefaultValue(true));
            modelBuilder.Entity<TenantUser>().HasMany(n => n.Roles).WithOne(n => n.User)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<RolePermission>().HasOne(n => n.Origin).WithMany(o => o.RoleInheritanceChildren)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<RolePermission>().HasOne(n => n.LinkedBy).WithMany(l => l.ResultingLinks)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<GRoleLRole>().HasOne(n => n.Origin).WithMany(o => o.RoleInheritanceChildren)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilder.Entity<GRoleLRole>().HasOne(n => n.LinkedBy).WithMany(l => l.ResultingGlobalLinks)
                .OnDelete(DeleteBehavior.ClientSetNull);
            modelBuilderOptions.ConfigureModelBuilder(modelBuilder);
        }
    }
}