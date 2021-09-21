using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.EFRepo.Extensions;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext
{
    public class SecurityContext : DbContext, IForeignKeyProvider
    {
        private readonly ILogger<SecurityContext> logger;
        private readonly IPermissionScope tenantProvider;
        private readonly IHttpContextAccessor httpContext;
        private readonly bool useFilters = false;

        public SecurityContext(DbContextOptions<SecurityContext> options) : base(options)
        {

        }

        public SecurityContext(IPermissionScope tenantProvider, IHttpContextAccessor httpContext, ILogger<SecurityContext> logger, DbContextOptions<SecurityContext> options) : base(options)
        {
            this.logger = logger;
            this.tenantProvider = tenantProvider;
            this.httpContext = httpContext;
            useFilters = true;

            try
            {
                logger.LogDebug($@"SecurityContext initialized. useFilters={useFilters}, CurrentTenant: {tenantProvider?.PermissionPrefix}");
            }
            catch
            {

            }
        }

        /// <summary>
        /// Indicates whether to switch off tenant filtering
        /// </summary>
        public bool ShowAllTenants { get; set; } = false;
        
        
        /// <summary>
        /// When tenant filtering is used, this hides tenant-relevant records that are NOT bound to a specific tenant
        /// </summary>
        public bool HideGlobals { get; set; } = false;

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
                
                return Tenants.First(n => n.TenantName == tenantProvider.PermissionPrefix).TenantId;
            }
        }

        /// <summary>
        /// Indicates whether there is a current http context
        /// </summary>
        public bool FilterAvailable => httpContext?.HttpContext != null;

        public DbSet<AuthenticationType> AuthenticationTypes { get;set; }

        public DbSet<User> Users { get; set; }

        public DbSet<DashboardWidget> Widgets { get; set; }

        public DbSet<UserWidget> UserWidgets { get; set; }

        public DbSet<CustomUserProperty> UserProperties { get; set; }

        public DbSet<Role> Roles { get;set; }

        public DbSet<Permission> Permissions { get; set; }

        public DbSet<UserRole> UserRoles { get; set; }

        public DbSet<RolePermission> RolePermissions { get;set; }

        public DbSet<Tenant> Tenants { get; set; }

        public DbSet<TenantSetting> TenantSettings { get; set; }
        
        public DbSet<TenantUser> TenantUsers { get; set; }

        public DbSet<NavigationMenu> Navigation { get; set; }

        public DbSet<TenantNavigationMenu> TenantNavigation { get;set; }

        public DbSet<WebPlugin> WebPlugins{get;set;}

        public DbSet<WebPluginConstant> WebPluginConstants { get; set; }

        public DbSet<DiagnosticsQuery> DiagnosticsQueries { get; set; }

        public DbSet<DiagnosticsQueryParameter> DiagnosticsQueryParameters { get; set; }

        public DbSet<TenantDiagnosticsQuery> TenantDiagnosticsQueries { get; set; }

        public DbSet<GlobalSetting> GlobalSettings { get; set; }

        public DbSet<SystemEvent> SystemLog { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.TableNamesFromProperties(this);
            modelBuilder.Entity<NavigationMenu>().Property(p => p.UrlUniqueness).HasComputedColumnSql("case when isnull(Url,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) else Url end persisted");
            modelBuilder.Entity<Role>().Property(p => p.RoleNameUniqueness).HasComputedColumnSql("'__T'+convert(varchar(10),TenantId)+'##'+RoleName persisted");
            modelBuilder.Entity<Permission>().Property(p => p.PermissionNameUniqueness).HasComputedColumnSql("case when TenantId is null then PermissionName else '__T'+convert(varchar(10),TenantId)+'##'+PermissionName end persisted");
            modelBuilder.Entity<WebPlugin>().Property(p => p.PluginNameUniqueness).HasComputedColumnSql("case when TenantId is null then UniqueName else '__T'+convert(varchar(10),TenantId)+'##'+UniqueName end persisted");
            modelBuilder.Entity<WebPluginConstant>().Property(p => p.NameUniqueness).HasComputedColumnSql("case when TenantId is null then Name else '__T'+convert(varchar(10),TenantId)+'##'+Name end persisted");
            modelBuilder.Entity<Role>().HasMany(n => n.RolePermissions).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            modelBuilder.Entity<Role>().HasMany(n => n.UserRoles).WithOne(p => p.Role).OnDelete(DeleteBehavior.ClientCascade);
            
            if (useFilters)
            {
                modelBuilder.Entity<Permission>().HasQueryFilter(pr => ShowAllTenants || !FilterAvailable || pr.TenantId != null && pr.Tenant.TenantName == tenantProvider.PermissionPrefix || pr.TenantId == null && !HideGlobals);
                modelBuilder.Entity<TenantNavigationMenu>().HasQueryFilter(nav => ShowAllTenants || !FilterAvailable || nav.Tenant.TenantName == tenantProvider.PermissionPrefix && (nav.PermissionId == null || nav.Permission.TenantId == null || nav.Permission.Tenant.TenantName==tenantProvider.PermissionPrefix ));
                modelBuilder.Entity<NavigationMenu>().HasQueryFilter(nav => string.IsNullOrEmpty(nav.Url) || ShowAllTenants || !FilterAvailable || nav.Tenants.Any(n => n.Tenant.TenantName == tenantProvider.PermissionPrefix) && ((nav.PermissionId == null || nav.EntryPoint.TenantId == null || nav.EntryPoint.Tenant.TenantName==tenantProvider.PermissionPrefix )));
                modelBuilder.Entity<RolePermission>().HasQueryFilter(perm => ShowAllTenants || !FilterAvailable || perm.Tenant.TenantName == tenantProvider.PermissionPrefix && perm.Permission != null);
                //--unfixed
                modelBuilder.Entity<DiagnosticsQuery>().HasQueryFilter(qry => ShowAllTenants || !FilterAvailable || qry.Tenants.Any(n => n.Tenant.TenantName == tenantProvider.PermissionPrefix));
                modelBuilder.Entity<DiagnosticsQueryParameter>().HasQueryFilter(param => ShowAllTenants || !FilterAvailable || param.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == tenantProvider.PermissionPrefix));
                modelBuilder.Entity<TenantDiagnosticsQuery>().HasQueryFilter(tdq => ShowAllTenants || !FilterAvailable || tdq.Tenant.TenantName == tenantProvider.PermissionPrefix);
                modelBuilder.Entity<TenantSetting>().HasQueryFilter(stt => ShowAllTenants || !FilterAvailable || stt.Tenant.TenantName == tenantProvider.PermissionPrefix);
                modelBuilder.Entity<TenantUser>().HasQueryFilter(tu => ShowAllTenants || !FilterAvailable || tu.Tenant.TenantName == tenantProvider.PermissionPrefix);
                modelBuilder.Entity<Role>().HasQueryFilter(ro => ShowAllTenants || !FilterAvailable || ro.Tenant.TenantName == tenantProvider.PermissionPrefix);
                modelBuilder.Entity<WebPlugin>().HasQueryFilter(wp => ShowAllTenants || !FilterAvailable || wp.TenantId != null && wp.Tenant.TenantName == tenantProvider.PermissionPrefix || wp.TenantId == null && !HideGlobals);
                modelBuilder.Entity<WebPluginConstant>().HasQueryFilter(wc => ShowAllTenants || !FilterAvailable || wc.TenantId != null && wc.Tenant.TenantName == tenantProvider.PermissionPrefix || wc.TenantId == null && !HideGlobals);
                modelBuilder.Entity<DashboardWidget>().HasQueryFilter(dw => ShowAllTenants || !FilterAvailable || dw.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == tenantProvider.PermissionPrefix));
                modelBuilder.Entity<UserWidget>().HasQueryFilter(uw => ShowAllTenants || !FilterAvailable || (uw.Widget.DiagnosticsQuery.Tenants.Any(n => n.Tenant.TenantName == tenantProvider.PermissionPrefix) && uw.Tenant.TenantName == tenantProvider.PermissionPrefix && uw.UserName == httpContext.HttpContext.User.Identity.Name));
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
                return (from t in Tenants orderby t.DisplayName select new ForeignKeyData<string>{Key=t.TenantName,Label=t.DisplayName, FullRecord=t.ToDictionary(true)}).ToList().Where(n => httpContext.HttpContext.RequestServices.VerifyUserPermissions(new []{n.Key}));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (httpContext?.HttpContext != null)
                {
                    var ret = (from t in Widgets.ToArray()
                        where httpContext.HttpContext.RequestServices.VerifyUserPermissions(new[]
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
                return (from t in Tenants orderby t.DisplayName select new ForeignKeyData<string>{Key=t.TenantName,Label=t.DisplayName, FullRecord = t.ToDictionary(true)}).ToList().Where(n => httpContext.HttpContext.RequestServices.VerifyUserPermissions(new []{n.Key}));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (httpContext?.HttpContext != null)
                {
                    var ret = (from t in Widgets.ToArray()
                        where httpContext.HttpContext.RequestServices.VerifyUserPermissions(new[]
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
                return (from t in Tenants where t.TenantId == tid select new ForeignKeyData<string>{Key=t.TenantName,Label=t.DisplayName, FullRecord = t.ToDictionary(true)}).ToList().Where(n => httpContext.HttpContext.RequestServices.VerifyUserPermissions(new []{n.Key}));
            }

            if (tableName == "AuthorizedWidgets")
            {
                if (httpContext?.HttpContext != null)
                {
                    var ret = (from t in Widgets.ToArray()
                        where httpContext.HttpContext.RequestServices.VerifyUserPermissions(new[]
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
