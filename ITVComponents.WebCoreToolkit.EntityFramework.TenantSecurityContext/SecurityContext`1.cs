using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using ITVComponents.EFRepo.DataAnnotations;
using ITVComponents.EFRepo.DbContextConfig.Expressions;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthenticationClaimMapping = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.AuthenticationClaimMapping;
using AuthenticationType = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.AuthenticationType;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.DashboardWidget;
using DiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.DiagnosticsQuery;
using DiagnosticsQueryParameter = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.DiagnosticsQueryParameter;
using GlobalSetting = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.GlobalSetting;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.Role;
using RolePermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.RolePermission;
using SystemEvent = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.SystemEvent;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Tenant;
using TenantDiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.TenantDiagnosticsQuery;
using TenantNavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.TenantNavigationMenu;
using TenantSetting = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.TenantSetting;
using TutorialStream = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.TutorialStream;
using User = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.User;
using WebPlugin = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.WebPlugin;
using Feature = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Feature;
using AppPermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.AppPermission;
using AppPermissionSet = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.AppPermissionSet;
using ClientAppTemplatePermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.ClientAppTemplatePermission;
using ClientAppTemplate = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.ClientAppTemplate;
using ClientApp = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.ClientApp;
using ClientAppPermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.ClientAppPermission;
using ClientAppUser = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models.ClientAppUser;
using ITVComponents.EFRepo.Options;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext
{
    [ExplicitlyExpose, DenyForeignKeySelection]
    public class SecurityContext<TImpl> : DbContext, IForeignKeyProvider, ISecurityContext<int,User,Role,Permission,UserRole,RolePermission,TenantUser,NavigationMenu,TenantNavigationMenu,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam, DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant, AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission, AppPermissionSet, ClientAppTemplatePermission, ClientApp, ClientAppPermission, ClientAppUser>
    where TImpl:SecurityContext<TImpl>
    {
        private readonly ILogger<TImpl> logger;
        protected readonly DbContextModelBuilderOptions<TImpl> modelBuilderOptions;
        private readonly IPermissionScope tenantProvider;
        private readonly IContextUserProvider userProvider;
        private readonly bool useFilters = false;
        private bool showAllTenants = false;
        private bool hideGlobals = false;
        private Stack<FullSecurityAccessHelper> securityStateStack = new Stack<FullSecurityAccessHelper>();
        private bool hideDisabledUsers = true;

        public SecurityContext(DbContextModelBuilderOptions<TImpl> modelBuilderOptions, DbContextOptions<TImpl> options) : base(options)
        {
            this.modelBuilderOptions = modelBuilderOptions;
        }

        public SecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider, ILogger<TImpl> logger, IOptions<DbContextModelBuilderOptions<TImpl>> modelBuilderOptions, DbContextOptions<TImpl> options) : base(options)
        {
            this.logger = logger;
            this.modelBuilderOptions = modelBuilderOptions.Value;
            this.tenantProvider = tenantProvider;
            this.userProvider = userProvider;
            useFilters = true;
            /*HideGlobals = true;
            HideGlobals = false;
            ShowAllTenants = false;
            ShowAllTenants = true;*/
            try
            {
                //logger.LogDebug($@"SecurityContext initialized. useFilters={useFilters}, CurrentTenant: {tenantProvider?.PermissionPrefix}, ShowAllTenants: {showAllTenants}, HideGlobals: {hideGlobals}");
                this.modelBuilderOptions.ConfigureExpressionProperty(()=>CurrentTenant);
                this.modelBuilderOptions.ConfigureExpressionProperty(()=>ShowAllTenants);
                this.modelBuilderOptions.ConfigureExpressionProperty(()=>FilterAvailable);
                this.modelBuilderOptions.ConfigureExpressionProperty(()=>HideGlobals);
                this.modelBuilderOptions.ConfigureExpressionProperty(()=>HideDisabledUsers);
                this.modelBuilderOptions.ConfigureExpressionProperty(()=>CurrentUserName);
                this.modelBuilderOptions.ConfigureExpressionProperty(() => CurrentTenantId);
            }
            catch
            {

            }
        }

        /// <summary>
        /// Gets a value indicating whetherthe context is configured to work with query-filters
        /// </summary>
        protected bool UseFilters => useFilters;

        /// <summary>
        /// Indicates whether to switch off tenant filtering
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

        /// <summary>
        /// Gets or sets a value indicating whether to select disabled users
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

        /// <summary>
        /// When tenant filtering is used, this hides tenant-relevant records that are NOT bound to a specific tenant
        /// </summary>
        [ExpressionPropertyRedirect("HideGlobals")]
        public bool HideGlobals
        {
            get => hideGlobals;
            set
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
            }
        }

        /// <summary>
        /// Gets the Id of the current Tenant. If no TenantProvider was provided, this value is null.
        /// </summary>
        [ExpressionPropertyRedirect("CurrentTenantId")]
        public int? CurrentTenantId
        {
            get
            {
                if (tenantProvider == null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(tenantProvider.PermissionPrefix))
                {
                    return null;
                }

                return Tenants.FirstOrDefault(n => n.TenantName.ToLower() == tenantProvider.PermissionPrefix.ToLower())?.TenantId;
            }
        }

        /// <summary>
        /// Indicates whether there is a current http context
        /// </summary>
        [ExpressionPropertyRedirect("FilterAvailable")]
        public bool FilterAvailable =>
            userProvider?.User != null && (userProvider.User.Identities.Any(i => i.IsAuthenticated));

        [ExpressionPropertyRedirect("CurrentUserName")]
        protected string CurrentUserName => userProvider.User?.Identity?.Name;

        [ExpressionPropertyRedirect("CurrentTenant")]
        private string CurrentTenant
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
        /// Gets the user that currently uses this Context
        /// </summary>
        protected IPrincipal Me => userProvider?.User;

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public DbSet<AuthenticationType> AuthenticationTypes { get;set; }
        public DbSet<AuthenticationClaimMapping> AuthenticationClaimMappings { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin)]
        public DbSet<User> Users { get; set; }

        public DbSet<DashboardWidget> Widgets { get; set; }

        public DbSet<DashboardParam> WidgetParams { get; set; }

        public DbSet<DashboardWidgetLocalization> WidgetLocales { get; set; }

        public DbSet<UserWidget> UserWidgets { get; set; }

        public DbSet<CustomUserProperty> UserProperties { get; set; }

        [ManualTableName("Roles")]
        public DbSet<Role> SecurityRoles { get;set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View", "DiagnosticsQueries.View", "DiagnosticsQueries.Write", "Permissions.SelectFK")]
        public DbSet<Permission> Permissions { get; set; }

        [ManualTableName("UserRoles")]
        public DbSet<UserRole> TenantUserRoles { get; set; }

        public DbSet<RolePermission> RolePermissions { get;set; }

        public DbSet<HealthScript> HealthScripts { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View")]
        public DbSet<Feature> Features { get; set; }

        public DbSet<TemplateModule> TemplateModules { get; set; }

        public DbSet<TemplateModuleConfigurator> TemplateModuleConfigurators { get; set; }
        public DbSet<TemplateModuleConfiguratorParameter> TemplateModuleConfiguratorParameters { get; set; }
        public DbSet<TemplateModuleScript> TemplateModuleScripts { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View", "DiagnosticsQueries.View", "DiagnosticsQueries.Write", "Tenants.SelectFK")]
        public DbSet<Tenant> Tenants { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Sysadmin")]
        public DbSet<TenantTemplate> TenantTemplates { get; set; }

        public DbSet<TenantSetting> TenantSettings { get; set; }

        public DbSet<TenantFeatureActivation> TenantFeatureActivations { get; set; }

        public DbSet<TenantUser> TenantUsers { get; set; }

        public DbSet<NavigationMenu> Navigation { get; set; }

        public DbSet<TenantNavigationMenu> TenantNavigation { get;set; }

        public DbSet<WebPlugin> WebPlugins{get;set;}

        public DbSet<WebPluginConstant> WebPluginConstants { get; set; }

        public DbSet<WebPluginGenericParameter> GenericPluginParams { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "DashboardWidgets.Write", "DashboardWidgets.View")]
        public DbSet<DiagnosticsQuery> DiagnosticsQueries { get; set; }

        public DbSet<DiagnosticsQueryParameter> DiagnosticsQueryParameters { get; set; }

        public DbSet<TenantDiagnosticsQuery> TenantDiagnosticsQueries { get; set; }
        public DbSet<AssetTemplate> AssetTemplates { get; set; }
        public DbSet<AssetTemplateFeature> AssetTemplateFeatures { get; set; }
        public DbSet<AssetTemplateGrant> AssetTemplateGrants { get; set; }
        public DbSet<AssetTemplatePath> AssetTemplatePathFilters { get; set; }
        public DbSet<SharedAsset> SharedAssets { get; set; }
        public DbSet<SharedAssetTenantFilter> SharedAssetTenantFilters { get; set; }
        public DbSet<SharedAssetUserFilter> SharedAssetUserFilters { get; set; }
        public DbSet<AppPermission> AppPermissions { get; set; }
        public DbSet<AppPermissionSet> AppPermissionSets { get; set; }
        public DbSet<ClientAppTemplatePermission> ClientAppTemplatePermissions { get; set; }
        public DbSet<ClientAppTemplate> ClientAppTemplates { get; set; }
        public DbSet<ClientAppPermission> ClientAppPermissions { get; set; }
        public DbSet<ClientApp> ClientApps { get; set; }
        public DbSet<ClientAppUser> ClientAppUsers { get; set; }

        public DbSet<GlobalSetting> GlobalSettings { get; set; }

        public DbSet<SystemEvent> SystemLog { get; set; }

        public DbSet<VideoTutorial> Tutorials { get; set; }

        public DbSet<TutorialStream> TutorialStreams { get; set; }

        public DbSet<TrustedFullAccessComponent> TrustedFullAccessComponents { get; set; }

        void IBaseTenantContext.RegisterSecurityRollback(FullSecurityAccessHelper fullSecurityAccessHelper)
        {
            if (!fullSecurityAccessHelper.CreatedWithContext)
            {
                throw new InvalidOperationException("Use Constructor with context argument, to use this method.");
            }

            securityStateStack.Push(new FullSecurityAccessHelper{ForwardHelper=fullSecurityAccessHelper,HideGlobals=hideGlobals,ShowAllTenants = showAllTenants});
            showAllTenants = fullSecurityAccessHelper.ShowAllTenants;
            hideGlobals = fullSecurityAccessHelper.HideGlobals;
        }

        void IBaseTenantContext.RollbackSecurity(FullSecurityAccessHelper fullSecurityAccessHelper)
        {
            var tmp = securityStateStack.Pop();
            if (tmp.ForwardHelper == fullSecurityAccessHelper)
            {
                showAllTenants = tmp.ShowAllTenants;
                hideGlobals = tmp.HideGlobals;
            }
            else
            {
                throw new InvalidOperationException("Invalid Disposal-order!");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.TableNamesFromProperties(this);
            modelBuilder.Entity<Role>().HasMany(n => n.RolePermissions).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<Role>().HasMany(n => n.UserRoles).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<TenantUser>(b => b.Property(n => n.Enabled).HasDefaultValue(true));
            modelBuilderOptions.ConfigureModelBuilder(modelBuilder);
        }

        /// <summary>
        /// Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        public IEnumerable GetForeignKeyFilterQuery(string tableName)
        {
            if (tableName == "TenantSelectionFk")
            {
                return (from t in Tenants orderby t.DisplayName select new ForeignKeyData<string>{Key=t.TenantName,Label=t.DisplayName, FullRecord=t.ToDictionary(true)}).ToList().Where(n => userProvider.Services.VerifyUserPermissions(new []{n.Key}));
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
        /// Gets the filter Linq-Query for the given table-name. If you implement this interface, form a query that uses the db-context as [db] and the search-string as [filter]
        /// </summary>
        /// <param name="tableName">the table-name for which to get the foreign-key data</param>
        /// <param name="postedFilter">a filter that was posted when a Foreignkey was queried with POST</param>
        /// <returns>the query that will be executed go get the foreignkey-data</returns>
        public IEnumerable GetForeignKeyFilterQuery(string tableName, Dictionary<string,object> postedFilter)
        {
            if (tableName == "TenantSelectionFk")
            {
                return (from t in Tenants orderby t.DisplayName select new ForeignKeyData<string>{Key=t.TenantName,Label=t.DisplayName, FullRecord = t.ToDictionary(true)}).ToList().Where(n => userProvider.Services.VerifyUserPermissions(new []{n.Key}));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (userProvider?.User!= null)
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

        public IEnumerable GetForeignKeyResolveQuery(string tableName, object id)
        {
            if (tableName == "TenantSelectionFk")
            {
                int tid = Convert.ToInt32(id);
                return (from t in Tenants where t.TenantId == tid select new ForeignKeyData<string>{Key=t.TenantName,Label=t.DisplayName, FullRecord = t.ToDictionary(true)}).ToList().Where(n => userProvider.Services.VerifyUserPermissions(new []{n.Key}));
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
    }
}
