using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.Handlers.Impl;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
// Dieselben Aliase wie im Basic-Kontext selbst: die Namen gibt es in Basic.Models UND in Shared.Models.
using CustomUserProperty = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.CustomUserProperty;
using DashboardParam = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.DashboardParam;
using DashboardWidget = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.DashboardWidget;
using DiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.DiagnosticsQuery;
using DiagnosticsQueryParameter = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.DiagnosticsQueryParameter;
using NavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.NavigationMenu;
using Permission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.Permission;
using Role = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.Role;
using RolePermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.RolePermission;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;
using TenantDiagnosticsQuery = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.TenantDiagnosticsQuery;
using TenantNavigationMenu = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.TenantNavigationMenu;
using User = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.User;
using AppPermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.AppPermission;
using AppPermissionSet = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.AppPermissionSet;
using ClientAppTemplate = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.ClientAppTemplate;
using ClientApp = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.ClientApp;
using ClientAppPermission = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.ClientAppPermission;
using ClientAppAccess = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models.ClientAppAccess;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test.ExternalServices
{
    /// <summary>
    /// Der Testkontext mit den globalen Filtern der Anwendung - ein EIGENER Typ, weil EF das Modell je Kontexttyp puffert:
    /// teilten sich gefilterte und ungefilterte Kontexte einen Typ, entschiede die Reihenfolge der Tests, welches Modell
    /// gilt.
    /// </summary>
    public sealed class FilteredExternalServiceTestContext : ExternalServiceTestContext
    {
        public FilteredExternalServiceTestContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<ExternalServiceTestContext> logger,
            IOptions<DbContextModelBuilderOptions<ExternalServiceTestContext>> modelBuilderOptions,
            DbContextOptions<ExternalServiceTestContext> options)
            : base(tenantProvider, userProvider, logger, modelBuilderOptions, options)
        {
        }
    }

    /// <summary>
    /// Der echte Basic-Sicherheitskontext, nur mit den beiden berechneten Spalten als gewoehnliche gefuehrt: der
    /// InMemory-Provider rechnet weder <c>lower(TenantName)</c> noch den eindeutigen Dienstnamen aus, und beide sind
    /// Pflichtfelder.
    /// </summary>
    public class ExternalServiceTestContext : EntityFramework.TenantSecurity.Basic.SecurityContext<ExternalServiceTestContext>
    {
        public ExternalServiceTestContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
            ILogger<ExternalServiceTestContext> logger,
            IOptions<DbContextModelBuilderOptions<ExternalServiceTestContext>> modelBuilderOptions,
            DbContextOptions<ExternalServiceTestContext> options)
            : base(tenantProvider, userProvider, logger, modelBuilderOptions, options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Tenant>().Property(t => t.TenantNameLower).ValueGeneratedNever();
            modelBuilder.Entity<FlatExternalOAuthService>().Property(t => t.CalculatedUniqueServiceName).ValueGeneratedNever();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ComputeServiceNames();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ComputeServiceNames();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Was die Datenbank als berechnete Spalte erledigt (Sql- bzw. PostgreSqlColumnsSyntaxHelper): 'GLOBAL'+Name bzw.
        /// 'T'+TenantId+'-'+Name. Ohne das scheitert jedes Anlegen ueber den Handler am Pflichtfeld.
        /// </summary>
        private void ComputeServiceNames()
        {
            foreach (var entry in ChangeTracker.Entries<FlatExternalOAuthService>()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified))
            {
                var svc = entry.Entity;
                svc.CalculatedUniqueServiceName = svc.TenantId == null
                    ? $"GLOBAL{svc.UniqueConnectionName}"
                    : $"T{svc.TenantId}-{svc.UniqueConnectionName}";
            }
        }

        /// <summary>Ein frischer Kontext auf der benannten InMemory-Ablage, mit festem Mandanten (null = keiner).</summary>
        /// <remarks>
        /// <b>Ohne die globalen Filter der Anwendung</b> (leere Modell-Optionen). Die Mandantengrenze der Handler-Tests
        /// liegt im Handler selbst; wer den Datenbankfilter pruefen will, nimmt <see cref="BuildFiltered"/>.
        /// </remarks>
        public static ExternalServiceTestContext Build(string databaseName, string? currentTenant)
            => new(new FixedScope(currentTenant), new AnonymousUser(false), NullLogger<ExternalServiceTestContext>.Instance,
                Microsoft.Extensions.Options.Options.Create(new DbContextModelBuilderOptions<ExternalServiceTestContext>()),
                new DbContextOptionsBuilder<ExternalServiceTestContext>().UseInMemoryDatabase(databaseName).Options);

        /// <summary>
        /// Derselbe Kontext MIT den globalen Filtern, wie die Anwendung sie registriert
        /// (<c>ConfigureGlobalFilters</c>), und mit angemeldetem Benutzer - erst dann greifen sie (<c>FilterAvailable</c>).
        /// </summary>
        public static FilteredExternalServiceTestContext BuildFiltered(string databaseName, string currentTenant)
        {
            var services = new ServiceCollection();
            services.ConfigureGlobalFilters<ExternalServiceTestContext>();
            var modelOptions = services.BuildServiceProvider()
                .GetRequiredService<IOptions<DbContextModelBuilderOptions<ExternalServiceTestContext>>>();
            return new FilteredExternalServiceTestContext(new FixedScope(currentTenant), new AnonymousUser(true),
                NullLogger<ExternalServiceTestContext>.Instance, modelOptions,
                new DbContextOptionsBuilder<ExternalServiceTestContext>().UseInMemoryDatabase(databaseName).Options);
        }

        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string? scope;

            public FixedScope(string? scope) => this.scope = scope;

            protected override string GetPermissionScopePrefix() => scope!;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException("The tenant of these tests is fixed.");
        }

        /// <summary>Der Kontext selbst braucht keinen Benutzer - die Rechte prueft der Handler ueber den Dienstanbieter.</summary>
        private sealed class AnonymousUser : IContextUserProvider
        {
            public AnonymousUser(bool signedIn)
                => User = new ClaimsPrincipal(signedIn
                    ? new ClaimsIdentity(new[] { new Claim(System.Security.Claims.ClaimTypes.Name, "tester") }, "Test")
                    : new ClaimsIdentity());

            public ClaimsPrincipal User { get; }

            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

            public string RequestPath => "/";

            public IServiceProvider Services => null!;
        }
    }

    /// <summary>
    /// Der Handler, geschlossen ueber den Basic-Kontext - so, wie ihn <c>AddMudBlazorTenantSecurityViews</c> fuer einen
    /// Basic-Host registriert.
    /// </summary>
    public sealed class ExternalServiceTestHandler : ExternalServiceAdminHandler<ExternalServiceTestContext, Tenant, int, User, Role,
        Permission, UserRole, RolePermission, TenantUser, RoleRole, GlobalRole, GlobalRolePermission, GRoleLRole, NavigationMenu,
        TenantNavigationMenu, DiagnosticsQuery, DiagnosticsQueryParameter, TenantDiagnosticsQuery, DashboardWidget, DashboardParam,
        DashboardWidgetLocalization, UserWidget, CustomUserProperty, AssetTemplate, AssetTemplatePath, AssetTemplateGrant,
        AssetTemplateFeature, SharedAsset, SharedAssetUserFilter, SharedAssetTenantFilter, ClientAppTemplate, AppPermission,
        AppPermissionSet, ClientApp, ClientAppPermission, ClientAppAccess, FlatWebPlugin, FlatWebPluginConstant,
        FlatWebPluginGenericParameter, FlatSequence, FlatTenantSetting, FlatTenantFeatureActivation, FlatExternalOAuthService,
        FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin, BaseTenantContextSecurityTrustConfig>
    {
        public ExternalServiceTestHandler(IDbContextFactory<ExternalServiceTestContext> dbFactory, IServiceProvider services,
            ISecurityRepository secRepo)
            : base(dbFactory, services, secRepo, NullLogger<IExternalServiceAdminHandler>.Instance)
        {
        }
    }
}
