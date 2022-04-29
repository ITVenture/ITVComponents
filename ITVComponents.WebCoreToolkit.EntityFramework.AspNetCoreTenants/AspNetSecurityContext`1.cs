using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AuthenticationClaimMapping = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.AuthenticationClaimMapping;
using AuthenticationType = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.AuthenticationType;
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DashboardWidget;
using DiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DiagnosticsQuery;
using DiagnosticsQueryParameter = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.DiagnosticsQueryParameter;
using GlobalSetting = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.GlobalSetting;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.Role;
using RolePermission = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.RolePermission;
using SystemEvent = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.SystemEvent;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.Tenant;
using TenantDiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.TenantDiagnosticsQuery;
using TenantNavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models.TenantNavigationMenu;
using TenantSetting = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.TenantSetting;
using TutorialStream = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.TutorialStream;
using WebPlugin = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models.WebPlugin;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants
{
    [ExplicitlyExpose, DenyForeignKeySelection]
    public class AspNetSecurityContext<TImpl> : IdentityDbContext<User>, IForeignKeyProvider, ISecurityContext<string, User, Role,Permission,UserRole,RolePermission,TenantUser,NavigationMenu,TenantNavigationMenu,DiagnosticsQuery,DiagnosticsQueryParameter,TenantDiagnosticsQuery,DashboardWidget,DashboardParam,UserWidget, CustomUserProperty> 
                                                where TImpl:AspNetSecurityContext<TImpl>
    {
        private readonly ILogger<TImpl> logger;
        private readonly IPermissionScope tenantProvider;
        private readonly IContextUserProvider userProvider;
        private readonly bool useFilters = false;
        private bool showAllTenants = false;
        private bool hideGlobals = false;
        private Stack<FullSecurityAccessHelper> securityStateStack = new Stack<FullSecurityAccessHelper>();
        private bool showDisabledUsers = true;

        public AspNetSecurityContext(DbContextOptions<TImpl> options) : base(options)
        {
        }

        public AspNetSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider, ILogger<TImpl> logger, DbContextOptions<TImpl> options) : base(options)
        {
            this.logger = logger;
            this.tenantProvider = tenantProvider;
            this.userProvider = userProvider;
            useFilters = true;
            /*HideGlobals = true;
            HideGlobals = false;
            ShowAllTenants = false;
            ShowAllTenants = true;*/
            try
            {
                logger.LogDebug($@"SecurityContext initialized. useFilters={useFilters}, CurrentTenant: {tenantProvider?.PermissionPrefix}, ShowAllTenants: {showAllTenants}, HideGlobals: {hideGlobals}");
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
        public bool HideDisabledUsers {
            get => showDisabledUsers;
            set
            {
                if (value != showDisabledUsers)
                {

                    if (value && FilterAvailable &&
                        !userProvider.Services.VerifyUserPermissions(new string[]
                            { ToolkitPermission.Sysadmin, ToolkitPermission.TenantAdmin }))
                    {
                        showDisabledUsers = false;
                    }
                    else
                    {
                        showDisabledUsers = value;
                    }
                }
            }
        }

        /// <summary>
        /// When tenant filtering is used, this hides tenant-relevant records that are NOT bound to a specific tenant
        /// </summary>
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

                return Tenants.FirstOrDefault(n => n.TenantName == tenantProvider.PermissionPrefix)?.TenantId;
            }
        }

        /// <summary>
        /// Indicates whether there is a current http context
        /// </summary>
        public bool FilterAvailable => userProvider?.User != null && (userProvider.User.Identity?.IsAuthenticated??false);

        private string CurrentTenant
        {
            get
            {
                string retVal = null;
                if (!showAllTenants)
                {
                    retVal = tenantProvider.PermissionPrefix;
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
        public override DbSet<User> Users { get; set; }

        public DbSet<DashboardWidget> Widgets { get; set; }

        public DbSet<DashboardParam> WidgetParams { get; set; }

        public DbSet<UserWidget> UserWidgets { get; set; }

        public DbSet<CustomUserProperty> UserProperties { get; set; }

        public DbSet<Role> SecurityRoles { get;set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View", "DiagnosticsQueries.View", "DiagnosticsQueries.Write", "Permissions.SelectFK")]
        public DbSet<Permission> Permissions { get; set; }

        public DbSet<UserRole> TenantUserRoles { get; set; }

        public DbSet<RolePermission> RolePermissions { get;set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "Navigation.Write", "Navigation.View")]
        public DbSet<Feature> Features { get; set; }

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

        public DbSet<WebPluginGenericParameter> GenericPluginParams { get; set; }

        public DbSet<WebPluginConstant> WebPluginConstants { get; set; }

        [ForeignKeySecurity(ToolkitPermission.Sysadmin, "DashboardWidgets.Write", "DashboardWidgets.View")]
        public DbSet<DiagnosticsQuery> DiagnosticsQueries { get; set; }

        public DbSet<DiagnosticsQueryParameter> DiagnosticsQueryParameters { get; set; }

        public DbSet<TenantDiagnosticsQuery> TenantDiagnosticsQueries { get; set; }

        public DbSet<GlobalSetting> GlobalSettings { get; set; }

        public DbSet<SystemEvent> SystemLog { get; set; }

        public DbSet<VideoTutorial> Tutorials { get; set; }

        public DbSet<TutorialStream> TutorialStreams { get; set; }

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
            modelBuilder.Entity<NavigationMenu>().Property(p => p.UrlUniqueness).HasComputedColumnSql("case when isnull(Url,'')='' and isnull(RefTag,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) when isnull(Url,'')='' then RefTag else Url end persisted");
            modelBuilder.Entity<Role>().Property(p => p.RoleNameUniqueness).HasComputedColumnSql("'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");
            modelBuilder.Entity<Permission>().Property(p => p.PermissionNameUniqueness).HasComputedColumnSql("case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");
            modelBuilder.Entity<WebPlugin>().Property(p => p.PluginNameUniqueness).HasComputedColumnSql("case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");
            modelBuilder.Entity<WebPluginConstant>().Property(p => p.NameUniqueness).HasComputedColumnSql("case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");
            modelBuilder.Entity<Role>().HasMany(n => n.RolePermissions).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<Role>().HasMany(n => n.UserRoles).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<TenantUser>(b => b.Property(n => n.Enabled).HasDefaultValue(true));
            if (useFilters)
            {
                modelBuilder.Entity<Permission>().HasQueryFilter(pr => showAllTenants || !FilterAvailable || pr.TenantId != null && pr.Tenant.TenantName == CurrentTenant || pr.TenantId == null && !hideGlobals);
                modelBuilder.Entity<TenantNavigationMenu>().HasQueryFilter(nav => showAllTenants || !FilterAvailable || nav.Tenant.TenantName == CurrentTenant && (nav.PermissionId == null || nav.Permission.TenantId == null || nav.Permission.Tenant.TenantName== CurrentTenant));
                modelBuilder.Entity<NavigationMenu>().HasQueryFilter(nav => string.IsNullOrEmpty(nav.Url) || showAllTenants || !FilterAvailable || nav.Tenants.Any(n => n.Tenant.TenantName == CurrentTenant) && ((nav.PermissionId == null || nav.EntryPoint.TenantId == null || nav.EntryPoint.Tenant.TenantName== CurrentTenant)));
                modelBuilder.Entity<RolePermission>().HasQueryFilter(perm => showAllTenants || !FilterAvailable || perm.Tenant.TenantName == CurrentTenant && perm.Permission != null);
                //--unfixed
                modelBuilder.Entity<DiagnosticsQuery>().HasQueryFilter(qry => showAllTenants || !FilterAvailable || qry.Tenants.Any(n => n.Tenant.TenantName == CurrentTenant));
                modelBuilder.Entity<DiagnosticsQueryParameter>().HasQueryFilter(param => showAllTenants || !FilterAvailable || param.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == CurrentTenant));
                modelBuilder.Entity<TenantDiagnosticsQuery>().HasQueryFilter(tdq => showAllTenants || !FilterAvailable || tdq.Tenant.TenantName == CurrentTenant);
                modelBuilder.Entity<TenantSetting>().HasQueryFilter(stt => showAllTenants || !FilterAvailable || stt.Tenant.TenantName == CurrentTenant);
                modelBuilder.Entity<TenantUser>().HasQueryFilter(tu => !FilterAvailable || (
                    (showAllTenants || tu.Tenant.TenantName == CurrentTenant)
                    && (showDisabledUsers || tu.Enabled)));
                modelBuilder.Entity<Role>().HasQueryFilter(ro => showAllTenants || !FilterAvailable || ro.Tenant.TenantName == CurrentTenant);
                modelBuilder.Entity<UserRole>().HasQueryFilter(ur => !FilterAvailable ||
                                                                     ((showAllTenants ||
                                                                       (ur.User.Tenant.TenantName ==
                                                                        CurrentTenant &&
                                                                        ur.Role.Tenant.TenantName ==
                                                                        CurrentTenant))
                                                                      && (showDisabledUsers || ur.User.Enabled)));
                modelBuilder.Entity<WebPlugin>().HasQueryFilter(wp => showAllTenants || !FilterAvailable || wp.TenantId != null && wp.Tenant.TenantName == CurrentTenant || wp.TenantId == null && !hideGlobals);
                modelBuilder.Entity<WebPluginGenericParameter>().HasQueryFilter(wp => showAllTenants || !FilterAvailable || wp.Plugin.TenantId != null && wp.Plugin.Tenant.TenantName == CurrentTenant || wp.Plugin.TenantId == null && !hideGlobals);
                modelBuilder.Entity<WebPluginConstant>().HasQueryFilter(wc => showAllTenants || !FilterAvailable || wc.TenantId != null && wc.Tenant.TenantName == CurrentTenant || wc.TenantId == null && !hideGlobals);
                modelBuilder.Entity<DashboardWidget>().HasQueryFilter(dw => showAllTenants || !FilterAvailable || dw.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == CurrentTenant));
                modelBuilder.Entity<DashboardParam>().HasQueryFilter(dw => showAllTenants || !FilterAvailable || dw.Parent.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == CurrentTenant));
                modelBuilder.Entity<UserWidget>().HasQueryFilter(uw => showAllTenants || !FilterAvailable || (uw.Widget.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == CurrentTenant) && uw.Tenant.TenantName == CurrentTenant && uw.UserName == userProvider.User.Identity.Name));
                modelBuilder.Entity<TenantFeatureActivation>().HasQueryFilter(fa => showAllTenants || !FilterAvailable || fa.Tenant.TenantName == CurrentTenant);
            }
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
