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
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
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

        // --- Externe Dienste: unbekannter Name -----------------------------------------------------

        [TestMethod]
        public void AnUnknownExternalService_IsNull_NotANullReference()
        {
            using var world = new World(dbName, TenantName, TrustTarget.ConcreteContext, signedIn: true);
            world.Seed();

            Assert.IsNull(world.Repository.GetExternalService("does-not-exist"));
            // Ein zweites Mal: ein unbekannter Name darf auch nicht als kaputter Puffereintrag liegen bleiben.
            Assert.IsNull(world.Repository.GetExternalService("does-not-exist"));
        }

        [TestMethod]
        public void AnUnknownExternalService_HasNoToken()
        {
            using var world = new World(dbName, TenantName, TrustTarget.ConcreteContext, signedIn: true);
            world.Seed();

            Assert.ThrowsExactly<InvalidOperationException>(() =>
                world.Repository.GetBufferedToken("does-not-exist", false, false, out _, out _));
        }

        // --- Externe Dienste: Zugangsdaten im Baum (Mechanismus der Settings) ----------------------
        // Baum: acme (Wurzel) > acme-a > acme-a1. Der Dienst ist global; die Zugangsdaten haengen an den Zeilen.

        private const string Child = "acme-a";
        private const string GrandChild = "acme-a1";

        [TestMethod]
        public void Credentials_OfTheRoot_ReachEveryDescendant_WhenInheritable()
        {
            using var world = new World(dbName, GrandChild, TrustTarget.ConcreteContext, signedIn: true);
            world.Seed();
            world.SeedCredentialTree(root: ("root-key", true));

            Assert.AreEqual("root-key", world.Repository.GetExternalService("svc", true).ClientSecret);
        }

        [TestMethod]
        public void Credentials_OfTheRoot_StayWithTheRoot_WhenNotInheritable()
        {
            using var world = new World(dbName, Child, TrustTarget.ConcreteContext, signedIn: true);
            world.Seed();
            world.SeedCredentialTree(root: ("root-key", false));

            Assert.AreNotEqual("root-key", world.Repository.GetExternalService("svc", true).ClientSecret);
        }

        [TestMethod]
        public void AnExclusiveException_HoldsOnlyForItsTenant()
        {
            // acme-a hat eigene, NICHT vererbbare Zugangsdaten: es selbst nimmt sie, sein Kind erbt weiter von der Wurzel.
            using (var child = new World(dbName, Child, TrustTarget.ConcreteContext, signedIn: true))
            {
                child.Seed();
                child.SeedCredentialTree(root: ("root-key", true), child: ("child-key", false));
                Assert.AreEqual("child-key", child.Repository.GetExternalService("svc", true).ClientSecret);
            }

            using var grandChild = new World(dbName, GrandChild, TrustTarget.ConcreteContext, signedIn: true);
            Assert.AreEqual("root-key", grandChild.Repository.GetExternalService("svc", true).ClientSecret);
        }

        [TestMethod]
        public void ABreakingException_HoldsForItsSubtree()
        {
            using var world = new World(dbName, GrandChild, TrustTarget.ConcreteContext, signedIn: true);
            world.Seed();
            world.SeedCredentialTree(root: ("root-key", true), child: ("child-key", true));

            Assert.AreEqual("child-key", world.Repository.GetExternalService("svc", true).ClientSecret);
        }

        [TestMethod]
        public void ThePlatformRow_IsUsedOnlyWhenTheDefinitionAllowsIt()
        {
            using (var allowed = new World(dbName, GrandChild, TrustTarget.ConcreteContext, signedIn: true))
            {
                allowed.Seed();
                allowed.SeedCredentialTree(platform: "platform-key", allowPlatform: true);
                Assert.AreEqual("platform-key", allowed.Repository.GetExternalService("svc", true).ClientSecret);
            }

            var other = $"{dbName}_noPlatform";
            using var refused = new World(other, GrandChild, TrustTarget.ConcreteContext, signedIn: true);
            refused.Seed();
            refused.SeedCredentialTree(platform: "platform-key", allowPlatform: false);
            Assert.AreNotEqual("platform-key", refused.Repository.GetExternalService("svc", true).ClientSecret);
        }

        [TestMethod]
        public void AnInheritedRow_BeatsThePlatformRow()
        {
            using var world = new World(dbName, GrandChild, TrustTarget.ConcreteContext, signedIn: true);
            world.Seed();
            world.SeedCredentialTree(root: ("root-key", true), platform: "platform-key", allowPlatform: true);

            Assert.AreEqual("root-key", world.Repository.GetExternalService("svc", true).ClientSecret);
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

            /// <param name="signedIn">
            /// ob ein angemeldeter Benutzer da ist. Die Maschinen-Tests laufen ohne; die Pfade, die den Mandantenfilter
            /// brauchen (<c>FilterAvailable</c>, etwa die externen Dienste), nur mit.
            /// </param>
            public World(string dbName, string currentTenant, TrustTarget trustTarget, bool signedIn = false)
            {
                this.trustTarget = trustTarget;
                var options = new DbContextOptionsBuilder<TreeTestContext>()
                    .UseInMemoryDatabase(dbName).Options;

                // Ein echter Container: DbSecurityAccessProvider holt sich seinen Ladekontext ueber die
                // IToolkitContextFactory aus dem Container, die ihn per ActivatorUtilities baut - es muessen
                // also alle Konstruktor-Abhaengigkeiten aufloesbar sein.
                var sc = new ServiceCollection();
                sc.AddSingleton<IPermissionScope>(new FixedScope(currentTenant));
                sc.AddSingleton<IContextUserProvider>(new NoUser(signedIn));
                sc.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
                sc.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
                sc.AddSingleton(Microsoft.Extensions.Options.Options.Create(
                    new DbContextModelBuilderOptions<TreeTestContext>()));
                sc.AddSingleton(options);
                sc.AddSingleton<ISecurityAccessProvider, DbSecurityAccessProvider>();
                sc.AddSingleton<IToolkitContextFactory>(sp => new TestContextFactory(sp));
                services = sc.BuildServiceProvider();

                var accessProvider = services.GetRequiredService<ISecurityAccessProvider>();
                Repository = new CoreIdentityTree.Security.AspNetDbTreeSecurityRepository<TreeTestContext>(
                    services.GetRequiredService<IToolkitContextFactory>(),
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

            /// <summary>
            /// Legt unter der Wurzel (<see cref="TenantName"/>) acme-a und acme-a1 an, dazu den globalen ApiKey-Dienst "svc"
            /// und die angegebenen Zugangsdaten-Zeilen. Verschluesselt wird mit dem Systemschluessel - die Testmandanten
            /// haben kein eigenes Passwort, und dann faellt auch der Mandantenschluessel darauf zurueck.
            /// </summary>
            public void SeedCredentialTree((string Key, bool Inheritable)? root = null,
                (string Key, bool Inheritable)? child = null, string platform = null, bool allowPlatform = false)
            {
                using var ctx = New();
                var rootTenant = ctx.Tenants.Single(n => n.TenantName == TenantName);
                var childTenant = new TreeModels.HierarchyTenant
                {
                    TenantName = Child, DisplayName = Child, TenantNameLower = Child, ParentTenantId = rootTenant.TenantId
                };
                ctx.Tenants.Add(childTenant);
                ctx.SaveChanges();
                ctx.Tenants.Add(new TreeModels.HierarchyTenant
                {
                    TenantName = GrandChild, DisplayName = GrandChild, TenantNameLower = GrandChild,
                    ParentTenantId = childTenant.TenantId
                });

                var service = new TreeModels.TreeModels.HierarchyExternalOAuthService
                {
                    UniqueConnectionName = "svc", CalculatedUniqueServiceName = "GLOBALsvc",
                    AuthorizationEndpoint = string.Empty, TokenEndpoint = string.Empty, RevocationEndpoint = string.Empty,
                    ClientId = "X-Key", ClientSecret = string.Empty, Scope = string.Empty, Global = true, TenantId = null,
                    AuthenticationType = ExternalServiceAuthenticationType.ApiKey, AllowPlatformCredentials = allowPlatform
                };
                ctx.ExternalOAuthServices.Add(service);
                ctx.SaveChanges();

                void Row(int? tenantId, string key, bool inheritable)
                    => ctx.ExternalOAuthServiceTenantLogins.Add(new TreeModels.TreeModels.HierarchyExternalOAuthServiceTenantLogin
                    {
                        OAuthServiceId = service.OAuthServiceId, TenantId = tenantId, Token = "{}", Inheritable = inheritable,
                        CredentialJson = ExternalServiceCredentials.Protect(new ExternalServiceCredential { Secret = key }, v => v.Encrypt())
                    });

                if (root is { } r) Row(rootTenant.TenantId, r.Key, r.Inheritable);
                if (child is { } c) Row(childTenant.TenantId, c.Key, c.Inheritable);
                if (platform != null) Row(null, platform, false);
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
            public NoUser(bool signedIn)
                => User = new ClaimsPrincipal(signedIn
                    ? new ClaimsIdentity(new[] { new Claim(System.Security.Claims.ClaimTypes.Name, "tester") }, "Test")
                    : new ClaimsIdentity());

            public ClaimsPrincipal User { get; }

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

                // Ausnahme: die Sicht "den Baum hinauf" wird aus der Mandantentabelle nachgebaut (bis drei Ebenen) - die
                // Zugangsdaten-Aufloesung geht ueber sie, wie Settings und Plugins.
                modelBuilder.Entity<Vm.UpwardsTenantView>().ToInMemoryQuery(() =>
                    Set<TreeModels.HierarchyTenant>().Select(t => new Vm.UpwardsTenantView
                        {
                            OutermostLeafTenantId = t.TenantId, OutermostLeafTenantName = t.TenantName,
                            ParentTenantId = t.TenantId, ParentTenantName = t.TenantName, ParentLevel = 1
                        })
                        .Concat(Set<TreeModels.HierarchyTenant>().Where(t => t.ParentTenantId != null)
                            .Select(t => new Vm.UpwardsTenantView
                            {
                                OutermostLeafTenantId = t.TenantId, OutermostLeafTenantName = t.TenantName,
                                ParentTenantId = t.ParentTenantId.Value, ParentTenantName = t.ParentTenant.TenantName,
                                ParentLevel = 2
                            }))
                        .Concat(Set<TreeModels.HierarchyTenant>()
                            .Where(t => t.ParentTenant != null && t.ParentTenant.ParentTenantId != null)
                            .Select(t => new Vm.UpwardsTenantView
                            {
                                OutermostLeafTenantId = t.TenantId, OutermostLeafTenantName = t.TenantName,
                                ParentTenantId = t.ParentTenant.ParentTenantId.Value,
                                ParentTenantName = t.ParentTenant.ParentTenant.TenantName, ParentLevel = 3
                            })));

                // Berechnete Spalte wie oben: der Test setzt den eindeutigen Dienstnamen selbst.
                modelBuilder.Entity<TreeModels.TreeModels.HierarchyExternalOAuthService>()
                    .Property(s => s.CalculatedUniqueServiceName).ValueGeneratedNever();
            }
        }
    }
}
