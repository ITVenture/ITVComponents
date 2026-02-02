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
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.TreeModels;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantTreeShared.Models.VirtualModels;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants
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
        private bool hideDisabledUsers = true;
        private bool hideGlobals = false;
        private bool includeChildTree = false;
        private bool includeParentTree = false;
        private bool showAllTenants = false;
        private int? currentTenantId;
        private string bufferedTenantName;
        private Dictionary<string, bool> componentSpecialTrusts;

        public AspNetTreeSecurityContext(DbContextModelBuilderOptions<TImpl> modelBuilderOptions,
            DbContextOptions<TImpl> options) : base(options)
        {
            this.modelBuilderOptions = modelBuilderOptions;
        }

        public AspNetTreeSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
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

                if (string.IsNullOrEmpty(CurrentTenant))
                {
                    return null;
                }

                if (currentTenantId != null && bufferedTenantName == CurrentTenant)
                {
                    return currentTenantId;
                }

                bufferedTenantName = tenantProvider.PermissionPrefix;
                return currentTenantId =Tenants.FirstOrDefault(n => n.TenantName.ToLower() == tenantProvider.PermissionPrefix.ToLower())
                    ?.TenantId;
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

            using (FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>.CreateForCaller(this, this,
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
        public DbSet<TenantSecurityShared.Models.Localization> Localizations { get; set; }

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
        public IEnumerable GetForeignKeyFilterQuery(string tableName)
        {
            if (tableName == "TenantSelectionFk")
            {
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
                }
            }

            /*if (tableName == "Permissions")
            {
                return from t in Permissions orderby t.PermissionName select new ForeignKeyData<int> {Key = t.PermissionId, Label = t.PermissionName};
            }*/

            return null;
        }

        /// <summary>
        ///     Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the
        ///     db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <param name="postedFilter">a filter that was posted when a Foreignkey was queried with POST</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        public IEnumerable GetForeignKeyFilterQuery(string tableName, Dictionary<string, object> postedFilter)
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

            return null;
        }

        public IEnumerable GetForeignKeyResolveQuery(string tableName, object id)
        {
            if (tableName == "TenantSelectionFk")
            {
                int tid = Convert.ToInt32(id);
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
                    return ret;
                }
            }

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

            using (FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>.CreateForCaller(this, this,
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

            using (FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>.CreateForCaller(this, this,
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
                            ToolkitPermission.Sysadmin, TenantTreeShared.Helpers.ToolkitPermission.BranchAdmin,
                            TenantTreeShared.Helpers.ToolkitPermission.BranchViewer
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
                            { ToolkitPermission.Sysadmin, TenantTreeShared.Helpers.ToolkitPermission.BranchAdmin }))
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

        Stack<FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>>
            ITrustfulComponent<HierarchyTenantContextSecurityTrustConfig>.securityStateStack { get; } =
            new Stack<FullSecurityAccessHelper<HierarchyTenantContextSecurityTrustConfig>>();

        [ExpressionPropertyRedirect("CurrentUserName")]
        public string CurrentUserName => userProvider.User?.Identity?.Name;

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