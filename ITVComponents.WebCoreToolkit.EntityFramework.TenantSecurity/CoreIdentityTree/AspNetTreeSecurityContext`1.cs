using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Expressions;
using ITVComponents.EFRepo.Expressions.Models;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.EFRepo.Options;
using ITVComponents.Helpers;
using ITVComponents.Json;
using ITVComponents.TypeConversion;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Dynamitey;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using IHierarchySecurityContext = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.IHierarchySecurityContext<ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.HierarchyTenant, string, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.User, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Role, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.Permission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.UserRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.RolePermission,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.HierarchyTenantUser, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.RoleRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GlobalRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GlobalRolePermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.GRoleLRole, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.NavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.TenantNavigationMenu, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DiagnosticsQuery,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DiagnosticsQueryParameter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.TenantDiagnosticsQuery, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardWidget, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardParam,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.DashboardWidgetLocalization, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.UserWidget, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.CustomUserProperty, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplate, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplatePath,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplateGrant, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AssetTemplateFeature, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAsset, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAssetUserFilter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.SharedAssetTenantFilter,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppTemplate, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AppPermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.AppPermissionSet, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppTemplatePermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientApp,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppPermission, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model.ClientAppUser, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPlugin, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPluginConstant,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyWebPluginGenericParameter, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchySequence, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyTenantSetting,
    ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyTenantFeatureActivation, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthService, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthServiceState, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.TreeModels.HierarchyExternalOAuthServiceTenantLogin, ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers.Models.HierarchyTenantContextSecurityTrustConfig>;
namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree
{
    [ExplicitlyExpose, DenyForeignKeySelection]
    public class AspNetTreeSecurityContext<TImpl> : IdentityDbContext<User>, IForeignKeyProvider,
        IHierarchySecurityContext<HierarchyTenant, string, User, Role, Permission, UserRole, RolePermission,
            HierarchyTenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu, TenantNavigationMenu, DiagnosticsQuery,
            DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam,
            DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath,
            AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter,
            ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp,
            ClientAppPermission, ClientAppUser, HierarchyWebPlugin, HierarchyWebPluginConstant,
            HierarchyWebPluginGenericParameter, HierarchySequence, HierarchyTenantSetting,
            HierarchyTenantFeatureActivation, HierarchyExternalOAuthService, HierarchyExternalOAuthServiceState, HierarchyExternalOAuthServiceTenantLogin, HierarchyTenantContextSecurityTrustConfig>
        where TImpl : AspNetTreeSecurityContext<TImpl>
    {
        protected readonly DbContextModelBuilderOptions<TImpl> modelBuilderOptions;
        private readonly ILogger<TImpl> logger;
        private readonly IPermissionScope tenantProvider;
        private readonly bool useFilters = false;
        private readonly IContextUserProvider userProvider;
        private readonly ISecurityAccessProvider securityAccessProvider;
        private bool hideDisabledUsers = true;
        private bool hideGlobals = false;
        private bool includeChildTree = false;
        private bool includeParentTree = false;
        private bool showAllTenants = false;
        private int? currentTenantId;
        private string bufferedTenantName;
        private bool currentTenantIdResolved;
        private bool resolvingCurrentTenantId;
        private Dictionary<string, bool> componentSpecialTrusts;

        public AspNetTreeSecurityContext(DbContextModelBuilderOptions<TImpl> modelBuilderOptions,
            DbContextOptions<TImpl> options) : base(options)
        {
            this.modelBuilderOptions = modelBuilderOptions;
        }

        public AspNetTreeSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ISecurityAccessProvider securityAccessProvider,
            ILogger<TImpl> logger, IOptions<DbContextModelBuilderOptions<TImpl>> modelBuilderOptions,
            DbContextOptions<TImpl> options) : base(options)
        {
            this.logger = logger;
            this.tenantProvider = tenantProvider;
            this.userProvider = userProvider;
            this.securityAccessProvider = securityAccessProvider;
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
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentTenantTree);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => IncludeParentTree);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => IncludeChildTree);
                //logger.LogDebug($@"SecurityContext initialized. useFilters={useFilters}, CurrentTenant: {tenantProvider?.PermissionPrefix}, ShowAllTenants: {showAllTenants}, HideGlobals: {hideGlobals}");
            }
            catch
            {
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

        private string CurrentTenant
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

        /// <summary>
        ///     Gets the Id of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
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
                    currentTenantId = ResolveTenantIdDetached(current);
                    currentTenantIdResolved = true;
                    return currentTenantId;
                }
                finally
                {
                    resolvingCurrentTenantId = false;
                }
            }
        }

        /// <summary>
        /// Resolves the current tenant's id by name on a dedicated, short-lived context instance instead of this
        /// shared circuit-scoped one. CurrentTenantId is read very frequently (every filtered query) and from
        /// parallel Blazor lifecycle callbacks; running the lookup on the shared instance can throw "a second
        /// operation was started on this context instance". The lookup is global (IgnoreQueryFilters) so it cannot
        /// re-enter CurrentTenantId on the fresh instance. Falls back to the shared instance when no IServiceProvider
        /// is available (e.g. the design-time factory / no user provider).
        /// </summary>
        private int? ResolveTenantIdDetached(string tenantNameLower)
        {
            var sp = userProvider?.Services;
            if (sp == null)
            {
                return Tenants.FirstOrDefault(n => n.TenantName.ToLower() == tenantNameLower)?.TenantId;
            }

            var ctx = (AspNetTreeSecurityContext<TImpl>)ActivatorUtilities.CreateInstance(sp, GetType());
            using (ctx)
            {
                return ctx.Tenants.IgnoreQueryFilters()
                    .FirstOrDefault(n => n.TenantName.ToLower() == tenantNameLower)?.TenantId;
            }
        }

        public DbSet<HierarchyWebPluginGenericParameter> GenericPluginParams { get; set; }

        public DbSet<HierarchyExternalOAuthServiceState> ExternalOAuthServiceStates { get; set; }

        public int SequenceNextVal(string sequenceName)
        {
            var trust = new HierarchyTenantContextSecurityTrustConfig
            {
                HideGlobals = false,
                IncludeParentTree = true,
                ShowAllTenants = showAllTenants
            };

            using (securityAccessProvider.CreateForCaller(this,
                       trust))
            {
                var mth = modelBuilderOptions.GetMethod<Func<DbContext, string, int, int>>("SequenceNextVal");
                if (mth == null)
                {
                    throw new InvalidOperationException("SequenceNextVal was not implemented for this Database-Type");
                }

                if (CurrentTenantId != null)
                {
                    var target = (from t in UpwardsTenantTreeView
                        join s in Sequences on t.ParentTenantId equals s.TenantId
                        where s.SequenceName == sequenceName && t.OutermostLeafTenantId == CurrentTenantId
                        orderby t.ParentLevel
                        select new { t.ParentLevel, t.ParentTenantId, s.SequenceName }).FirstOrDefault();
                    if (target != null)
                    {
                        return mth(this, target.SequenceName, target.ParentTenantId);
                    }
                }

                return -1;
            }
        }

        public DbSet<UserAccessTree<string>> UserAccessTree { get; set; }

        public DbSet<HierarchySequence> Sequences { get; set; }
        public DbSet<HierarchyExternalOAuthService> ExternalOAuthServices { get; set; }

        public DbSet<HierarchyExternalOAuthServiceTenantLogin> ExternalOAuthServiceTenantLogins { get; set; }

        public DbSet<HierarchyTenantFeatureActivation> TenantFeatureActivations { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View",
            "DiagnosticsQueries.View", "DiagnosticsQueries.Write", "Tenants.SelectFK")]
        public DbSet<HierarchyTenant> Tenants { get; set; }

        public DbSet<HierarchyTenantSetting> TenantSettings { get; set; }
        public DbSet<HierarchyWebPluginConstant> WebPluginConstants { get; set; }
        public DbSet<HierarchyWebPlugin> WebPlugins { get; set; }

        public DbSet<AuthenticationClaimMapping> AuthenticationClaimMappings { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public DbSet<AuthenticationType> AuthenticationTypes { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "DbResources.View", "DbResources.Write")]
        public DbSet<Culture> Cultures { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View")]
        public DbSet<Feature> Features { get; set; }

        [ExpressionPropertyRedirect("FilterAvailable")]
        public bool FilterAvailable =>
            userProvider?.User != null && (userProvider.User.Identities.Any(i => i.IsAuthenticated));

        public DbSet<GlobalSetting> GlobalSettings { get; set; }
        public DbSet<HealthScript> HealthScripts { get; set; }

        /// <summary>
        ///     When tenant filtering is used, this hides tenant-relevant records that are NOT bound to a specific tenant
        /// </summary>
        [ExpressionPropertyRedirect("HideGlobals")]
        public bool HideGlobals
        {
            get => hideGlobals;
            set => hideGlobals = value;
            /*set
            {
                if (value != hideGlobals)
                {
                    var tmp = hideGlobals;
                    hideGlobals = value;
                    if (!value && FilterAvailable &&
                        !userProvider.Services.VerifyUserPermissions(new[] { ToolkitPermission.Sysadmin }))
                    {
                        hideGlobals = tmp;
                    }
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

            if (tableName == "Tenants")
            {
                postedFilter.TryGetValue("CurrentTenantId", out var currentTenant);
                int cti = 0;
                if (TypeConverter.TryConvert(currentTenant, typeof(int), out var ctt))
                {
                    cti = (int)ctt;
                }

                int[] excludes = Array.Empty<int>();
                if (cti != 0)
                {
                    var ict = includeChildTree;
                    var sat = showAllTenants;
                    includeChildTree = true;
                    showAllTenants = true;
                    try
                    {
                        excludes = (from t in DownwardsTenantTreeView
                            where t.TopmostTenantId == cti
                            select t.ChildTenantId).ToArray();
                    }
                    finally
                    {
                        includeChildTree = ict;
                        showAllTenants = sat;
                    }
                }

                var rt = (from t in Tenants where !excludes.Contains(t.TenantId) select t);
                if (clientFilter != null)
                {
                    rt = rt.Where(ExpressionBuilder.BuildExpression<HierarchyTenant>(clientFilter, c =>
                    {
                        if (c == "Label")
                        {
                            return new[] { "TenantName", "DisplayName" };
                        }

                        return null;
                    }));
                }

                keyType = typeof(int);
                return rt.Select(t => new ForeignKeyData<int>
                    { Key = t.TenantId, Label = t.DisplayName, FullRecord = t.ToDictionary(true) });
            }

            if (tableName == nameof(SecurityRoles))
            {
                var icpt = includeParentTree;
                var sat = showAllTenants;
                includeParentTree= true;
                showAllTenants = true;
                IEnumerable<ForeignKeyData<int>> resultingRoles; 
                try
                {
                    int main = 0;
                    int par = 0;
                    resultingRoles = (SecurityRoles.Include(n => n.Tenant).Where(ExpressionBuilder.BuildExpression<Role>(clientFilter, c =>
                    {
                        if (c == "Label")
                        {
                            return new[] { nameof(Role.RoleName) };
                        }

                        return null;
                    }, reconfigureFilter: f =>
                    {
                        if (f.ProcessingInfo is not FilterProcessingHelper { Processed: true } && f is CompareFilter
                            {
                                PropertyName: nameof(Role.TenantId), Operator: CompareOperator.Equal
                            } cff)
                        {
                            var mainTenant = Convert.ToInt32(cff.Value);
                            var ct = Tenants.First(n => n.TenantId == mainTenant);
                            main = mainTenant;
                            if (ct.ParentTenantId != null)
                            {
                                par = ct.ParentTenantId.Value;
                                return new CompositeFilter()
                                {
                                    ProcessingInfo = new FilterProcessingHelper { Processed = true },
                                    Children =
                                    [
                                        new CompareFilter
                                        {
                                            Value = main,
                                            Operator = CompareOperator.Equal,
                                            ProcessingInfo = new FilterProcessingHelper { Processed = true },
                                            PropertyName = nameof(Role.TenantId)
                                        },
                                        new CompareFilter
                                        {
                                            Value = par,
                                            Operator = CompareOperator.Equal,
                                            ProcessingInfo = new FilterProcessingHelper { Processed = true },
                                            PropertyName = nameof(Role.TenantId)
                                        }
                                    ],
                                    Operator = BoolOperator.Or
                                };
                            }
                        }

                        return null;
                    })).Select(r => new ForeignKeyData<int>
                    {
                        FullRecord = r.ToDictionary(true),
                        Key = r.RoleId,
                        Label = (r.TenantId == main)?r.RoleName:$"{r.Tenant.DisplayName} -- {r.RoleName}"
                    })).ToArray();
                    keyType = typeof(int);
                    return resultingRoles;
                }
                finally
                {
                    includeParentTree= icpt;
                    showAllTenants = sat;
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

        public IEnumerable<HierarchyTenant> ChildTenantsWith(string userId, string currentTenant, string[] requiredPermissions)
        {
            var trust = new HierarchyTenantContextSecurityTrustConfig
            {
                HideGlobals = false,
                IncludeParentTree = true,
                IncludeChildTree = true,
                ShowAllTenants = true
            };

            using (securityAccessProvider.CreateForCaller(this,
                       trust))
            {
                var mth =
                    modelBuilderOptions.GetMethod<Func<DbContext, string, string, string[], HierarchyTenant[]>>(
                        "ChildTenantsWith");
                if (mth == null)
                {
                    throw new InvalidOperationException("ChildTenantsWith was not implemented for this Database-Type");
                }

                if (!string.IsNullOrEmpty(currentTenant))
                {
                    var tmpRet = mth(this, userId, currentTenant, requiredPermissions);
                    return tmpRet;
                }

                return Array.Empty<HierarchyTenant>();
            }
        }

        public IEnumerable<HierarchyTenant> ChildTenantsWith(string[] userLabels, string currentTenant, string[] requiredPermissions)
        {
            var trust = new HierarchyTenantContextSecurityTrustConfig
            {
                HideGlobals = false,
                IncludeParentTree = true,
                IncludeChildTree = true,
                ShowAllTenants = true
            };

            using (securityAccessProvider.CreateForCaller(this,
                       trust))
            {
                var mth =
                    modelBuilderOptions.GetMethod<Func<DbContext, string[], string, string[], HierarchyTenant[]>>(
                        "ChildTenantsWith");
                if (mth == null)
                {
                    throw new InvalidOperationException("ChildTenantsWith was not implemented for this Database-Type");
                }

                if (!string.IsNullOrEmpty(currentTenant))
                {
                    var tmpRet = mth(this, userLabels, currentTenant, requiredPermissions);
                    return tmpRet;
                }

                return Array.Empty<HierarchyTenant>();
            }
        }

        //--

        public IEnumerable<HierarchyTenant> ChildTenantsWith(string userId, int? currentTenantId, string[] requiredPermissions)
        {
            var trust = new HierarchyTenantContextSecurityTrustConfig
            {
                HideGlobals = false,
                IncludeParentTree = true,
                IncludeChildTree = true,
                ShowAllTenants = true
            };

            using (securityAccessProvider.CreateForCaller(this,
                       trust))
            {
                var mth =
                    modelBuilderOptions.GetMethod<Func<DbContext, string, int?, string[], HierarchyTenant[]>>(
                        "ChildTenantsWith");
                if (mth == null)
                {
                    throw new InvalidOperationException("ChildTenantsWith was not implemented for this Database-Type");
                }

                if (currentTenantId != null)
                {
                    var tmpRet = mth(this, userId, currentTenantId, requiredPermissions);
                    return tmpRet;
                }

                return Array.Empty<HierarchyTenant>();
            }
        }

        public IEnumerable<HierarchyTenant> ChildTenantsWith(string[] userLabels, int? currentTenantId, string[] requiredPermissions)
        {
            var trust = new HierarchyTenantContextSecurityTrustConfig
            {
                HideGlobals = false,
                IncludeParentTree = true,
                IncludeChildTree = true,
                ShowAllTenants = true
            };

            using (securityAccessProvider.CreateForCaller(this,
                       trust))
            {
                var mth =
                    modelBuilderOptions.GetMethod<Func<DbContext, string[], int?, string[], HierarchyTenant[]>>(
                        "ChildTenantsWith");
                if (mth == null)
                {
                    throw new InvalidOperationException("ChildTenantsWith was not implemented for this Database-Type");
                }

                if (currentTenantId != null)
                {
                    var tmpRet = mth(this, userLabels, currentTenantId, requiredPermissions);
                    return tmpRet;
                }

                return Array.Empty<HierarchyTenant>();
            }
        }

        [EFRepo.DataAnnotations.DbFunction("GetUpwardsRoleTreeForId")]
        public IQueryable<UpwardsRoleUserView<string>> GetUpwardsTenantUserRoles(string userId, string? leafTenant)
        {
            return FromExpression(() => GetUpwardsTenantUserRoles(userId, leafTenant));
        }

        [EFRepo.DataAnnotations.DbFunction("GetUpwardsRoleTreeForLabels")]
        public IQueryable<UpwardsRoleUserView<string>> GetUpwardsTenantUserLabelsRoles(string userLabelsJson, string? leafTenant)
        {
            return FromExpression(() => GetUpwardsTenantUserLabelsRoles(userLabelsJson, leafTenant));
        }

        [EFRepo.DataAnnotations.DbFunction("GetUpwardsRoleTreeForIdByLeafId")]
        public IQueryable<UpwardsRoleUserView<string>> GetUpwardsTenantUserRoles(string userId, int? leafTenantId)
        {
            return FromExpression(() => GetUpwardsTenantUserRoles(userId, leafTenantId));
        }

        [EFRepo.DataAnnotations.DbFunction("GetUpwardsRoleTreeForLabelsByLeafId")]
        public IQueryable<UpwardsRoleUserView<string>> GetUpwardsTenantUserLabelsRoles(string userLabelsJson, int? leafTenantId)
        {
            return FromExpression(() => GetUpwardsTenantUserLabelsRoles(userLabelsJson, leafTenantId));
        }

        public IQueryable<UpwardsTenantView> GetAccessibleUpwardsTenants(string[] userLabels, string authenticationType,
            int leafTenantId)
        {
            var lbl = (from t in userLabels select t.ToLower()).ToArray();
            var tmp = (from t in Users.Where(n => lbl.Contains(n.UserName.ToLower()) &&
                                                  (n.AuthenticationType == null ||
                                                   n.AuthenticationType.AuthenticationTypeName == authenticationType))
                join r in UserAccessTree on t.Id equals r.UserId
                where r.OutermostLeafTenantId == leafTenantId
                orderby r.ParentLevel
                select new UpwardsTenantView
                {
                    OutermostLeafTenantId = r.OutermostLeafTenantId,
                    OutermostLeafTenantName = r.OutermostLeafTenantName,
                    ParentLevel = r.ParentLevel,
                    ParentTenantId = r.ParentTenantId,
                    ParentTenantName = r.ParentTenantName
                });
            /*var lbl = (from t in userLabels select t.ToLower()).ToArray();
            var tmp = (from t in Users.Where(n => lbl.Contains(n.UserName.ToLower()) &&
                                                  (n.AuthenticationType == null ||
                                                   n.AuthenticationType.AuthenticationTypeName == authenticationType))
                        .Join(TenantUsers, u => u.Id, u => u.UserId, (tu, tt) => new { tt.TenantUserId, tt.TenantId })
                        .Join(((IHierarchySecurityContext)this).GetUpwardsTenantUserRoles(userLabels, leafTenantId),
                            l => l.TenantUserId, r => r.TenantUserId,
                            (l, r) => new { l.TenantUserId, l.TenantId, r.OutermostLeafTenantId, r.OutermostLeafTenantName, r.ParentLevel })
                        .Join(Tenants, l => l.OutermostLeafTenantId, r => r.TenantId, (l, r) => new { l, r })
                    select new
                    {
                        t.r.TenantId,
                        t.r.TenantName,
                        t.r.DisplayName,
                        DirectlyAssigned = t.l.TenantId == t.r.TenantId,
                        t.l.ParentLevel,
                        t.l.OutermostLeafTenantId,
                        t.l.OutermostLeafTenantName
                    }).Distinct()
                .OrderBy(n => n.ParentLevel)
                .Select(r => new UpwardsTenantView
                {
                    ParentLevel = r.ParentLevel,
                    OutermostLeafTenantId = r.OutermostLeafTenantId,
                    OutermostLeafTenantName = r.OutermostLeafTenantName,
                    ParentTenantId = r.TenantId,
                    ParentTenantName = r.TenantName
                });*/
            return tmp;
        }

        public IQueryable<UpwardsTenantView> GetAccessibleUpwardsTenants(string[] userLabels, string authenticationType, string leafTenant)
        {
            var lbl = (from t in userLabels select t.ToLower()).ToArray();
            var tmp = (from t in Users.Where(n => lbl.Contains(n.UserName.ToLower()) &&
                                                   (n.AuthenticationType == null ||
                                                    n.AuthenticationType.AuthenticationTypeName == authenticationType))
                join r in UserAccessTree on t.Id equals r.UserId
                       where r.OutermostLeafTenantName == leafTenant
                orderby r.ParentLevel
                        select new UpwardsTenantView
                {
                    OutermostLeafTenantId = r.OutermostLeafTenantId,
                    OutermostLeafTenantName = r.OutermostLeafTenantName,
                    ParentLevel = r.ParentLevel,
                    ParentTenantId = r.ParentTenantId,
                    ParentTenantName = r.ParentTenantName
                });
            /*var tmp = (from t in Users.Where(n => lbl.Contains(n.UserName.ToLower()) &&
                                                  (n.AuthenticationType == null ||
                                                   n.AuthenticationType.AuthenticationTypeName == authenticationType))
                    .Join(TenantUsers, u => u.Id, u => u.UserId, (tu, tt) => new { tt.TenantUserId, tt.TenantId })
                    .Join(((IHierarchySecurityContext)this).GetUpwardsTenantUserRoles(userLabels, leafTenant),
                        l => l.TenantUserId, r => r.TenantUserId,
                        (l, r) => new { l.TenantUserId, l.TenantId, r.OutermostLeafTenantId, r.OutermostLeafTenantName, r.ParentLevel })
                    .Join(Tenants, l => l.OutermostLeafTenantId, r => r.TenantId, (l, r) => new { l, r })
                select new
                {
                    t.r.TenantId, t.r.TenantName, t.r.DisplayName, DirectlyAssigned = t.l.TenantId == t.r.TenantId, t.l.ParentLevel, t.l.OutermostLeafTenantId, t.l.OutermostLeafTenantName 
                }).Distinct()
                .OrderBy(n => n.ParentLevel)
                .Select(r => new UpwardsTenantView
                {
                    ParentLevel = r.ParentLevel, 
                    OutermostLeafTenantId = r.OutermostLeafTenantId,
                    OutermostLeafTenantName = r.OutermostLeafTenantName,
                    ParentTenantId = r.TenantId,
                    ParentTenantName = r.TenantName
                });*/
            return tmp;
        }

        public IEnumerable<DownwardsUserRoleView<string>> GetDownwardsTenantUserRoles(string userId, bool userIdIsLabels, string viewpointTenant)
        {
            return
                Set<DownwardsUserRoleView<string>>()
                    .FromSqlInterpolated(
                        $"EXEC [GetDownwardsRoleTreeProc] {userId}, {userIdIsLabels}, {viewpointTenant}").ToList(); //sql(() => GetDownwardsTenantUserRoles(userId, userIdIsLabels, viewpointTenant));
        }

        [ExpressionPropertyRedirect("CurrentTenantTree")]
        public IQueryable<int> CurrentTenantTree => IncludeParentTree
            ? (from t in UpwardsTenantTreeView
                where t.OutermostLeafTenantName == CurrentTenant
                orderby t.ParentLevel
                select t.ParentTenantId)
            : Array.Empty<int>().AsQueryable();

        public DbSet<DownwardsTenantView> DownwardsTenantTreeView { get; set; }

        public bool IncludeChildTree
        {
            get { return includeChildTree; }
            set
            {
                if (value != includeChildTree)
                {
                    var tmp = includeChildTree;
                    includeChildTree = true;
                    if (value && !userProvider.Services.VerifyUserPermissions(new string[]
                        {
                            ToolkitPermission.Sysadmin, TreeShared.Helpers.ToolkitPermission.BranchAdmin,
                            TreeShared.Helpers.ToolkitPermission.BranchViewer
                        }))
                    {
                        includeChildTree = tmp;
                    }
                    else
                    {
                        includeChildTree = value;
                    }
                }
            }
        }

        public bool IncludeParentTree
        {
            get => includeParentTree;
            set
            {
                if (value != includeParentTree)
                {
                    var tmp = includeParentTree;
                    includeParentTree = true;
                    if (value && !userProvider.Services.VerifyUserPermissions(new string[]
                            { ToolkitPermission.Sysadmin, TreeShared.Helpers.ToolkitPermission.BranchAdmin }))
                    {
                        includeParentTree = tmp;
                    }
                    else
                    {
                        includeParentTree = value;
                    }
                }
            }
        }

        public DbSet<UpwardsTenantView> UpwardsTenantTreeView { get; set; }
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
        public DbSet<HierarchyTenantUser> TenantUsers { get; set; }
        public DbSet<CustomUserProperty> UserProperties { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public override DbSet<User> Users { get; set; }

        public DbSet<UserWidget> UserWidgets { get; set; }
        public DbSet<DashboardWidgetLocalization> WidgetLocales { get; set; }
        public DbSet<DashboardParam> WidgetParams { get; set; }

        public DbSet<DashboardWidget> Widgets { get; set; }

        IDictionary<string, bool> ITrustfulComponent<HierarchyTenantContextSecurityTrustConfig>.ComponentSpecialTrusts => componentSpecialTrusts;

        void ITrustfulComponent<HierarchyTenantContextSecurityTrustConfig>.ApplyTrust(
            HierarchyTenantContextSecurityTrustConfig trust)
        {
            includeParentTree = trust.IncludeParentTree;
            showAllTenants = trust.ShowAllTenants;
            hideGlobals = trust.HideGlobals;
            includeChildTree = trust.IncludeChildTree;
            componentSpecialTrusts = trust.SpecialFilterSettings == null
                ? null
                : new Dictionary<string, bool>(trust.SpecialFilterSettings);
        }

        HierarchyTenantContextSecurityTrustConfig ITrustfulComponent<HierarchyTenantContextSecurityTrustConfig>.
            GetReverseTrust(HierarchyTenantContextSecurityTrustConfig forwardTrustConfig)
        {
            return new HierarchyTenantContextSecurityTrustConfig
            {
                HideGlobals = hideGlobals,
                IncludeParentTree = includeParentTree,
                ShowAllTenants = showAllTenants,
                IncludeChildTree = includeChildTree,
                SpecialFilterSettings = componentSpecialTrusts == null
                    ? null
                    : new Dictionary<string, bool>(componentSpecialTrusts)
            };
        }

        Stack<IFullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>>
            ITrustfulComponent<HierarchyTenantContextSecurityTrustConfig>.securityStateStack { get; } =
            new Stack<IFullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>>();

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

            // Tree-Strategie: NUR HierarchyTenant ist die gemappte Tenant-Entity (Tabelle "Tenants").
            // Der konkrete Basistyp Tenant darf NICHT als eigene Entity ins Modell, sonst entsteht eine
            // TPH-Hierarchie Tenant<-HierarchyTenant, deren Tabelle nach dem Root ("Tenant") benannt wird
            // -> "Invalid object name 'Tenant'" zur Laufzeit + Snapshot-Drift. Zur Designtime wird Tenant
            // ohnehin nicht eingezogen; zur Laufzeit kann jedoch die frühe (filtergetriebene) Konvention-
            // Auflösung von *.Tenant-Navigationen den schlüsseldefinierenden Basistyp einziehen. Ignore
            // fixiert HierarchyTenant als alleinigen Root in beiden Welten.
            modelBuilder.Ignore<Shared.Models.Tenant>();

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
            modelBuilder.Entity<HierarchyTenantUser>(b => b.Property(n => n.Enabled).HasDefaultValue(true));
            modelBuilder.Entity<HierarchyTenantUser>().HasMany(n => n.Roles).WithOne(n => n.User)
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