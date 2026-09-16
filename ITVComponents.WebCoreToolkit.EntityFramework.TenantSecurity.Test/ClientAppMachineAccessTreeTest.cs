using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tree = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model;
using TreeModels = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models;
using Vm = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Models.VirtualModels;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Dasselbe wie <see cref="ClientAppMachineAccessTest"/>, aber im <b>hierarchischen</b> Kontext - und
    /// mit dem Aufbau, den ein KONSUMENT hat: der Sicherheits-Kontext liegt in einer anderen Assembly als
    /// das Repository, das ihn benutzt.
    /// </summary>
    /// <remarks>
    /// Genau daran haengt hier alles. Der hierarchische Pfad hat einen Schritt, den der flache nicht hat:
    /// <c>securityAccessProvider.CreateForCaller(securityContext, …)</c>. Der gewaehrt Vertrauen
    /// <b>implizit</b>, solange Aufrufer und Kontext dieselbe Assembly teilen - im Toolkit-eigenen Test ist
    /// das immer so, und dann prueft man genau das nicht, worauf es beim Konsumenten ankommt. Liegt der
    /// Kontext dagegen woanders (jeder echte Konsument), entscheidet ein Eintrag in
    /// <c>TrustedFullAccessComponents</c> - und der wird ZEICHENGENAU ueber ein Paar aus zwei
    /// assembly-qualifizierten Namen gesucht.
    /// <para>
    /// Deshalb erbt der Testkontext hier von <see cref="AspNetTreeSecurityContext{TImpl}"/> und lebt in der
    /// Test-Assembly: das ist die Konsumenten-Lage, nachgestellt.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ClientAppMachineAccessTreeTest
    {
        private const string TenantName = "acme";
        private const string MachineLabel = "Kasse1-5684110770ae4622871d502f402abc58";
        private const string ServicePermission = "ActAsService";
        private const string AuthType = "API Key";

        private string dbName;

        [TestInitialize]
        public void Setup() => dbName = $"machineAccessTree_{Guid.NewGuid():N}";

        // --- Der Pfad, den der Konsument geht ---------------------------------------------------

        [TestMethod]
        public void AMachineAccess_CountsAsAuthenticated_WhenTheTrustEntryNamesTheConcreteContext()
        {
            using var world = new World(dbName, TenantName, TrustTarget.ConcreteContext);
            world.Seed();

            Assert.IsTrue(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "mit passendem Vertrauens-Eintrag muss der hierarchische Pfad dasselbe sagen wie der flache.");
        }

        [TestMethod]
        public void AMachineAccess_GetsThePermissionSetsOfItsApplication()
        {
            using var world = new World(dbName, TenantName, TrustTarget.ConcreteContext);
            world.Seed();

            var perms = world.Repository.GetPermissions(Labels(MachineLabel), AuthType)
                .Select(n => n.PermissionName).ToArray();

            CollectionAssert.Contains(perms, ServicePermission);
        }

        // --- Die Falle -------------------------------------------------------------------------

        [TestMethod]
        public void ATrustEntryNamingTheToolkitBaseClass_StillLetsTheMachineAccessThrough()
        {
            using var world = new World(dbName, TenantName, TrustTarget.ToolkitBaseClass);
            world.Seed();

            // Der Eintrag GREIFT hier nicht: nachgeschlagen wird zeichengenau gegen
            // trustingObject.GetType().AssemblyQualifiedName, und das ist der Kontext des Konsumenten,
            // nicht die Toolkit-Basisklasse. Das Protokoll sagt das auch ("No Trust Configuration found
            // for the caller ... No special permissions will be granted").
            // Entscheidend ist die Wirkung: der Maschinenzugang gilt TROTZDEM. Der gewuenschte Trust wird
            // angewandt, der Eintrag haette ihn nur erweitern koennen. Wer das zuschnuert, macht aus
            // einem fehlenden Eintrag einen rechtelosen Dienst - und das sieht dann von aussen aus wie
            // ein ungueltiger Zugang.
            Assert.IsTrue(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "ein nicht passender Vertrauens-Eintrag darf den Maschinenzugang nicht aussperren.");
        }

        [TestMethod]
        public void WithoutAnyTrustEntry_TheMachineAccessStillCounts()
        {
            using var world = new World(dbName, TenantName, TrustTarget.None);
            world.Seed();

            Assert.IsTrue(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "dasselbe ohne jeden Eintrag: der Vertrauens-Mechanismus entscheidet ueber ZUSAETZLICHE " +
                "Rechte, nicht darueber, ob ein gueltiger Zugang gilt.");
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private static string[] Labels(string label)
            => [label, string.Format(Global.AppUserKeyIndicatorFormat, label)];

        /// <summary>Worauf der Vertrauens-Eintrag zeigt - das ist hier die eigentliche Versuchsanordnung.</summary>
        private enum TrustTarget
        {
            /// <summary>Gar kein Eintrag.</summary>
            None,

            /// <summary>Auf den Kontext des Konsumenten - so ist es richtig.</summary>
            ConcreteContext,

            /// <summary>Auf die Toolkit-Basisklasse - so seedet man es versehentlich.</summary>
            ToolkitBaseClass
        }

        private sealed class World : IDisposable
        {
            private readonly ServiceProvider services;
            private readonly TrustTarget trustTarget;

            public World(string dbName, string currentTenant, TrustTarget trustTarget)
            {
                this.trustTarget = trustTarget;
                var options = new DbContextOptionsBuilder<TreeTestContext>()
                    .UseInMemoryDatabase(dbName).Options;

                // Ein echter Container: DbSecurityAccessProvider baut sich seinen Ladekontext selbst ueber
                // ActivatorUtilities, es muessen also alle Konstruktor-Abhaengigkeiten aufloesbar sein.
                var sc = new ServiceCollection();
                sc.AddSingleton<IPermissionScope>(new FixedScope(currentTenant));
                sc.AddSingleton<IContextUserProvider>(new NoUser());
                sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
                sc.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                sc.AddSingleton(Microsoft.Extensions.Options.Options.Create(
                    new DbContextModelBuilderOptions<TreeTestContext>()));
                sc.AddSingleton(options);
                sc.AddSingleton<ISecurityAccessProvider, DbSecurityAccessProvider>();
                sc.AddTransient<Shared.ICoreSystemContext>(sp =>
                    ActivatorUtilities.CreateInstance<TreeTestContext>(sp));
                services = sc.BuildServiceProvider();

                var accessProvider = services.GetRequiredService<ISecurityAccessProvider>();
                Repository = new CoreIdentityTree.Security.AspNetDbTreeSecurityRepository<TreeTestContext>(
                    new TestContextFactory(services),
                    accessProvider,
                    Microsoft.Extensions.Options.Options.Create(new ExternalOAuthServiceBufferingOptions()),
                    NullLogger<CoreIdentityTree.Security.AspNetDbTreeSecurityRepository<TreeTestContext>>.Instance);
            }

            public CoreIdentityTree.Security.AspNetDbTreeSecurityRepository<TreeTestContext> Repository { get; }

            public void Seed()
            {
                using var ctx = New();
                SeedTrust(ctx);

                var tenant = new TreeModels.HierarchyTenant
                {
                    TenantName = TenantName, DisplayName = TenantName,
                    TenantNameLower = TenantName.ToLowerInvariant()
                };
                ctx.Tenants.Add(tenant);
                ctx.SaveChanges();

                var template = new Tree.ClientAppTemplate { Name = "POS-Agent" };
                ctx.ClientAppTemplates.Add(template);
                ctx.SaveChanges();

                var permission = new Tree.Permission
                {
                    PermissionName = ServicePermission, TenantId = tenant.TenantId,
                    PermissionNameUniqueness = $"{tenant.TenantId}:{ServicePermission}"
                };
                ctx.Permissions.Add(permission);
                ctx.SaveChanges();

                var set = new Tree.AppPermissionSet
                {
                    ClientAppTemplateId = template.ClientAppTemplateId, Name = "Geraetedienst"
                };
                ctx.AppPermissionSets.Add(set);
                ctx.SaveChanges();

                ctx.AppPermissions.Add(new Tree.AppPermission
                {
                    AppPermissionSetId = set.AppPermissionSetId, PermissionId = permission.PermissionId
                });

                var app = new Tree.ClientApp
                {
                    TenantId = tenant.TenantId, ClientAppTemplateId = template.ClientAppTemplateId,
                    ClientName = "POS-Agent", ClientKey = "NywHKJr3", ClientSecret = "not-verified-here",
                    Enabled = true, CreatedUtc = DateTime.UtcNow
                };
                ctx.ClientApps.Add(app);
                ctx.SaveChanges();

                ctx.ClientAppPermissions.Add(new Tree.ClientAppPermission
                {
                    ClientAppId = app.ClientAppId, AppPermissionSetId = set.AppPermissionSetId
                });
                ctx.ClientAppAccesses.Add(new Tree.ClientAppAccess
                {
                    ClientAppId = app.ClientAppId,
                    TenantUserId = null,
                    Label = MachineLabel,
                    DeviceLabel = "Kasse 1",
                    SecretHash = "not-verified-here",
                    CreatedUtc = DateTime.UtcNow
                });
                ctx.SaveChanges();
            }

            /// <summary>
            /// Legt den Vertrauens-Eintrag so an, wie ihn ein Konsument seedet: als Zeile mit zwei
            /// assembly-qualifizierten Namen.
            /// </summary>
            private void SeedTrust(TreeTestContext ctx)
            {
                if (trustTarget == TrustTarget.None)
                {
                    return;
                }

                var target = trustTarget == TrustTarget.ConcreteContext
                    ? typeof(TreeTestContext).AssemblyQualifiedName
                    : typeof(AspNetTreeSecurityContext<>).AssemblyQualifiedName;

                ctx.TrustedFullAccessComponents.Add(new TrustedFullAccessComponent
                {
                    FullQualifiedTypeName = typeof(CoreIdentityTree.Security.AspNetDbTreeSecurityRepository<>)
                        .BaseType!.GetGenericTypeDefinition().AssemblyQualifiedName,
                    TargetQualifiedTypeName = target,
                    Description = "Test",
                    TrustLevelConfig = "{\"ShowAllTenants\":true,\"HideGlobals\":false,\"IncludeParentTree\":true}"
                });
                ctx.SaveChanges();
            }

            private TreeTestContext New()
                => ActivatorUtilities.CreateInstance<TreeTestContext>(services);

            public void Dispose() => services.Dispose();
        }

        private sealed class TestContextFactory : IToolkitContextFactory
        {
            private readonly IServiceProvider services;

            public TestContextFactory(IServiceProvider services) => this.services = services;

            public T Create<T>() where T : class
                => (T)(object)ActivatorUtilities.CreateInstance<TreeTestContext>(services);

            public IContextLease<T> Lease<T>() where T : class
            {
                var ctx = ActivatorUtilities.CreateInstance<TreeTestContext>(services);
                return new ContextLease<T>((T)(object)ctx, ctx);
            }

            private sealed class ContextLease<T> : IContextLease<T> where T : class
            {
                private readonly IDisposable owner;

                public ContextLease(T context, IDisposable owner)
                {
                    Context = context;
                    this.owner = owner;
                }

                public T Context { get; }

                public void Dispose() => owner.Dispose();
            }
        }

        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string scope;

            public FixedScope(string scope) => this.scope = scope;

            protected override string GetPermissionScopePrefix() => scope;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException();
        }

        private sealed class NoUser : IContextUserProvider
        {
            public ClaimsPrincipal User { get; } = new(new ClaimsIdentity());

            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

            public string RequestPath => "/";

            public IServiceProvider Services => null;
        }

        /// <summary>
        /// Der hierarchische Kontext eines Konsumenten: erbt vom Toolkit, lebt aber in DIESER Assembly -
        /// und ist damit fuer den Vertrauens-Mechanismus ein fremder Typ, genau wie beim echten Anwender.
        /// </summary>
        public class TreeTestContext : AspNetTreeSecurityContext<TreeTestContext>
        {
            public TreeTestContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
                ISecurityAccessProvider securityAccessProvider, ILogger<TreeTestContext> logger,
                Microsoft.Extensions.Options.IOptions<DbContextModelBuilderOptions<TreeTestContext>> modelBuilderOptions,
                DbContextOptions<TreeTestContext> options)
                : base(tenantProvider, userProvider, securityAccessProvider, logger, modelBuilderOptions, options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                // Berechnete Spalte: der InMemory-Provider rechnet lower(TenantName) nicht aus, der
                // Testhelfer setzt den Wert selbst.
                modelBuilder.Entity<TreeModels.HierarchyTenant>()
                    .Property(t => t.TenantNameLower).ValueGeneratedNever();

                // Die Baum-Sichten sind in der Datenbank SICHTEN bzw. Ergebnisse von Tabellenfunktionen -
                // schluessellos, und ihre Konfiguration haengt am relationalen Zweig, den der
                // InMemory-Provider nicht laeuft. Ohne das hier scheitert schon der Modellaufbau
                // ("requires a primary key"). Eingesammelt wird pauschal, was keinen Schluessel hat: die
                // Liste dieser Typen ist laenger, als man beim Schreiben denkt (UpwardsTenantView,
                // DownwardsTenantView, DownwardsUserRoleView, UserAccessTree, …) und sie waechst.
                // Sie bleiben in diesen Tests leer - der Maschinenzweig fasst sie nicht an, er kennt
                // keinen Benutzer, dessen Baum man aufsteigen muesste.
                foreach (var keyless in modelBuilder.Model.GetEntityTypes()
                             .Where(n => n.FindPrimaryKey() == null).ToList())
                {
                    modelBuilder.Entity(keyless.ClrType).HasNoKey();
                }
            }
        }
    }
}
