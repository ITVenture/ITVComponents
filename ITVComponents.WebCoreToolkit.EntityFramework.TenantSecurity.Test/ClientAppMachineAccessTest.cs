using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sec = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Nagelt fest, dass ein Client-App-Zugang im <b>Maschinenmodus</b> - also einer OHNE
    /// <c>TenantUserId</c> - fuer sich steht: er gilt als angemeldet, und er bekommt die Rechte-Buendel
    /// seiner Anwendung direkt, ohne Umweg ueber einen Benutzer.
    /// </summary>
    /// <remarks>
    /// Der Anlass ist ein Bericht aus einem Konsumenten (PRE246): ein POS-Agent koppelt sauber, wird am
    /// Hub authentifiziert und darf sich dann nicht registrieren - <c>ActAsService</c> wird verweigert.
    /// Der Bericht vermutete den Maschinenzweig als fehlend. Diese Tests pruefen genau das an der echten
    /// Klasse nach: traegt der Zweig, liegt die Ursache woanders.
    /// <para>
    /// <b>Warum der echte Kontext und InMemory:</b> das Repository loest seine ~45 Typparameter ueber die
    /// volle Schliessung des Sicherheits-Kontextes auf, ein verkleinerter Kontext wird dabei gar nicht
    /// erkannt. Und die Rechte-Abfragen projizieren korrelierte Teilmengen, was sich zu APPLY / LATERAL
    /// uebersetzt - das kann SQLite nicht. Siehe RoleInheritanceDuplicateTest, dieselbe Begruendung.
    /// </para>
    /// <para>
    /// <b>Was diese Tests NICHT koennen:</b> der InMemory-Provider wertet client-seitig aus, ein
    /// Uebersetzungsfehler faellt hier also nie auf. Wer eine dieser Abfragen anfasst, braucht dafuer
    /// zusaetzlich einen Lauf gegen eine echte Datenbank.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ClientAppMachineAccessTest
    {
        private const string TenantName = "acme";
        private const string MachineLabel = "Kasse1-5684110770ae4622871d502f402abc58";
        private const string ServicePermission = "ActAsService";
        private const string AuthType = "API Key";

        private string dbName;

        [TestInitialize]
        public void Setup() => dbName = $"machineAccess_{Guid.NewGuid():N}";

        // --- Der gemeldete Fall ---------------------------------------------------------------

        [TestMethod]
        public void AMachineAccess_CountsAsAuthenticated()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true);

            Assert.IsTrue(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "ein Zugang ohne TenantUserId IST die Identitaet - es gibt keinen Benutzer, gegen den man " +
                "pruefen koennte, und der Zugang selbst ist bereits auf Widerruf, Ablauf und abgeschaltete " +
                "Anwendung geprueft.");
        }

        [TestMethod]
        public void AMachineAccess_GetsThePermissionSetsOfItsApplication()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true);

            var perms = world.Repository.GetPermissions(Labels(MachineLabel), AuthType)
                .Select(n => n.PermissionName).ToArray();

            CollectionAssert.Contains(perms, ServicePermission,
                "die Buendel der Anwendung gelten im Maschinenmodus DIREKT - ein Kassenterminal hat keine " +
                "Rollen, aus denen sich etwas schneiden liesse.");
        }

        // --- Die Gegenproben ------------------------------------------------------------------

        [TestMethod]
        public void ARevokedMachineAccess_DoesNotCount()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true, revoked: true);

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "der Widerruf ist die einzige Handhabe gegen ein abhanden gekommenes Geraet.");
        }

        [TestMethod]
        public void AnExpiredMachineAccess_DoesNotCount()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true, expiresUtc: DateTime.UtcNow.AddDays(-1));

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType));
        }

        [TestMethod]
        public void AMachineAccessOfADisabledApplication_DoesNotCount()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true, appEnabled: false);

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "die Anwendung abzuschalten muss alle ihre Geraete stillegen, nicht nur die neuen.");
        }

        [TestMethod]
        public void AMachineAccessOfAnotherTenant_DoesNotCount()
        {
            var world = new World(dbName, "other");
            world.Seed(machine: true);

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "der Mandant haengt an der ANWENDUNG - seit TenantUserId optional ist, gibt es keinen " +
                "Benutzer mehr, ueber den er sonst herkaeme.");
        }

        [TestMethod]
        public void AnUnknownLabel_DoesNotCount()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true);

            Assert.IsFalse(
                world.Repository.IsAuthenticated(Labels("Kasse1-dd0b0427139c4f208f5c3e06cfdb1db3"), AuthType),
                "genau das passiert, wenn ein Geraet den Schluessel einer FRUEHEREN Kopplung weiterbenutzt.");
        }

        // --- Der Puffer ------------------------------------------------------------------------

        [TestMethod]
        public void AnAccessCreatedAfterAFailedCheck_IsSeenOnceTheAnswerExpires()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true, withAccess: false);

            // Die Reihenfolge des gemeldeten Falls: das Geraet fragt an, BEVOR es gekoppelt ist.
            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "ohne Zugang gibt es kein Ja - das ist der Ausgangszustand, nicht der Befund.");

            world.AddAccess();
            world.Clock.Advance(TimeSpan.FromSeconds(10));

            Assert.IsTrue(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "nach der Kopplung muss der Zugang gelten. Frueher galt die gemerkte Antwort bis zum " +
                "IEntityChangeSignal - und das ist ein OPTIONALER Dienst. Wo keiner registriert ist " +
                "(Hintergrunddienst, gRPC-Hub), sperrte ein einmal gemerktes Nein das Geraet bis zum " +
                "Prozessende aus. Genau so gemeldet worden.");
        }

        [TestMethod]
        public void WithinItsLifetime_TheAnswerIsStillBuffered()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true, withAccess: false);

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType));

            world.AddAccess();
            world.Clock.Advance(TimeSpan.FromSeconds(1));

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "die Gegenprobe zum Test darueber: der Puffer ist gegen die Reentry-Stuerme paralleler " +
                "Blazor-Lifecycle-Callbacks da und MUSS innerhalb seiner Frist greifen. Faellt dieser " +
                "Test, puffert gar nichts mehr und jede Rechtefrage geht an die Datenbank.");
        }

        [TestMethod]
        public void ARevocationTakesEffect_OnceTheAnswerExpires()
        {
            var world = new World(dbName, TenantName);
            world.Seed(machine: true);

            Assert.IsTrue(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType));

            world.RevokeAccess();
            world.Clock.Advance(TimeSpan.FromSeconds(10));

            Assert.IsFalse(world.Repository.IsAuthenticated(Labels(MachineLabel), AuthType),
                "die schwerere Haelfte desselben Fehlers: ein gemerktes JA liess einen WIDERRUFENEN " +
                "Zugang weiterlaufen. Der Widerruf ist die einzige Handhabe gegen ein abhanden " +
                "gekommenes Geraet - er muss ohne Change-Signal und ohne Neustart greifen.");
        }

        // --- Testgeruest ----------------------------------------------------------------------

        /// <summary>Die Bezeichner, die eine Anwendung traegt: der blanke Name UND die App-User-Wicklung.</summary>
        private static string[] Labels(string label)
            => [label, string.Format(Global.AppUserKeyIndicatorFormat, label)];

        /// <summary>
        /// Mandant, Anwendung, Rechte-Buendel und Zugang - der kleinste Aufbau, in dem ein Maschinenzugang
        /// ueberhaupt etwas duerfen kann.
        /// </summary>
        private sealed class World
        {
            private readonly DbContextOptions<TestSecurityContext> options;
            private readonly string currentTenant;

            public World(string dbName, string currentTenant)
            {
                this.currentTenant = currentTenant;
                options = new DbContextOptionsBuilder<TestSecurityContext>()
                    .UseInMemoryDatabase(dbName).Options;
                var factory = new TestContextFactory(options, currentTenant);
                Clock = new TestClock();
                Repository = new TestRepository(factory, Clock);
            }

            public TestRepository Repository { get; }

            /// <summary>Die Uhr des Repositories - vorspulen statt schlafen.</summary>
            public TestClock Clock { get; }

            public void Seed(bool machine, bool revoked = false, DateTime? expiresUtc = null,
                bool appEnabled = true, bool withAccess = true)
            {
                using var ctx = New();
                // TenantNameLower von Hand: die Spalte ist in der Datenbank BERECHNET, der InMemory-Provider
                // rechnet sie nicht aus - ohne den Wert scheitert der Insert an "Required properties are
                // missing", und der Mandantenfilter fragt genau diese Spalte ab.
                var tenant = new Tenant
                {
                    TenantName = TenantName, DisplayName = TenantName,
                    TenantNameLower = TenantName.ToLowerInvariant()
                };
                ctx.Tenants.Add(tenant);
                ctx.SaveChanges();

                var template = new Sec.ClientAppTemplate { Name = "POS-Agent" };
                ctx.ClientAppTemplates.Add(template);
                ctx.SaveChanges();

                var permission = new Sec.Permission
                {
                    PermissionName = ServicePermission, TenantId = tenant.TenantId,
                    PermissionNameUniqueness = $"{tenant.TenantId}:{ServicePermission}"
                };
                ctx.Permissions.Add(permission);
                ctx.SaveChanges();

                var set = new Sec.AppPermissionSet
                {
                    ClientAppTemplateId = template.ClientAppTemplateId, Name = "Geraetedienst"
                };
                ctx.AppPermissionSets.Add(set);
                ctx.SaveChanges();

                ctx.AppPermissions.Add(new Sec.AppPermission
                {
                    AppPermissionSetId = set.AppPermissionSetId, PermissionId = permission.PermissionId
                });

                var app = new Sec.ClientApp
                {
                    TenantId = tenant.TenantId, ClientAppTemplateId = template.ClientAppTemplateId,
                    ClientName = "POS-Agent", ClientKey = "NywHKJr3", ClientSecret = "not-verified-here",
                    Enabled = appEnabled,
                    CreatedUtc = DateTime.UtcNow
                };
                ctx.ClientApps.Add(app);
                ctx.SaveChanges();

                ctx.ClientAppPermissions.Add(new Sec.ClientAppPermission
                {
                    ClientAppId = app.ClientAppId, AppPermissionSetId = set.AppPermissionSetId
                });
                ctx.SaveChanges();

                if (withAccess)
                {
                    AddAccess(ctx, machine, revoked, expiresUtc);
                }
            }

            /// <summary>Haengt den Zugang nachtraeglich an - fuer die Reihenfolge "erst fragen, dann koppeln".</summary>
            public void AddAccess()
            {
                using var ctx = New();
                AddAccess(ctx, machine: true, revoked: false, expiresUtc: null);
            }

            /// <summary>Widerruft den Zugang nachtraeglich - der Fall "Geraet abhanden gekommen".</summary>
            public void RevokeAccess()
            {
                using var ctx = New();
                var access = ctx.ClientAppAccesses.First(n => n.Label == MachineLabel);
                access.RevokedUtc = DateTime.UtcNow;
                ctx.SaveChanges();
            }

            private static void AddAccess(TestSecurityContext ctx, bool machine, bool revoked,
                DateTime? expiresUtc)
            {
                var app = ctx.ClientApps.First();
                ctx.ClientAppAccesses.Add(new Sec.ClientAppAccess
                {
                    ClientAppId = app.ClientAppId,
                    // Der Maschinenmodus IST die fehlende TenantUserId - mehr unterscheidet ihn nicht.
                    TenantUserId = null,
                    Label = MachineLabel,
                    DeviceLabel = "Kasse 1",
                    SecretHash = "not-verified-here",
                    CreatedUtc = DateTime.UtcNow,
                    ExpiresUtc = expiresUtc,
                    RevokedUtc = revoked ? DateTime.UtcNow : null
                });
                ctx.SaveChanges();
            }

            private TestSecurityContext New() => TestContextFactory.Build(options, currentTenant);
        }

        /// <summary>
        /// Das echte Repository, nur mit steuerbarer Uhr: der Puffer soll nachweislich ABLAUFEN, und ein
        /// Test, der dafuer fuenf Sekunden verschlaeft, wird irgendwann uebersprungen.
        /// </summary>
        private sealed class TestRepository : Basic.Security.DbSecurityRepository<TestSecurityContext>
        {
            private readonly TimeProvider clock;

            public TestRepository(Shared.DependencyInjection.IToolkitContextFactory contextFactory,
                TimeProvider clock)
                : base(contextFactory,
                    NullLogger<Basic.Security.DbSecurityRepository<TestSecurityContext>>.Instance)
                => this.clock = clock;

            protected override TimeProvider Clock => clock;
        }

        /// <summary>Eine Uhr, die nur vorwaerts geht, wenn der Test es sagt.</summary>
        public sealed class TestClock : TimeProvider
        {
            private DateTimeOffset now = new(2026, 9, 16, 20, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => now;

            public void Advance(TimeSpan by) => now = now.Add(by);
        }

        /// <summary>
        /// Reicht bei jedem Vorgang einen frischen Kontext heraus - genau wie die echte Fabrik. Die
        /// InMemory-Ablage haengt am Namen, alle Kontexte sehen also dieselben Daten.
        /// </summary>
        private sealed class TestContextFactory : Shared.DependencyInjection.IToolkitContextFactory
        {
            private readonly DbContextOptions<TestSecurityContext> options;
            private readonly string currentTenant;

            public TestContextFactory(DbContextOptions<TestSecurityContext> options, string currentTenant)
            {
                this.options = options;
                this.currentTenant = currentTenant;
            }

            public static TestSecurityContext Build(DbContextOptions<TestSecurityContext> options,
                string currentTenant)
                => new(new FixedScope(currentTenant), new NoUser(),
                    NullLogger<TestSecurityContext>.Instance,
                    Microsoft.Extensions.Options.Options.Create(new DbContextModelBuilderOptions<TestSecurityContext>()), options);

            public T Create<T>() where T : class => (T)(object)Build(options, currentTenant);

            public Shared.DependencyInjection.IContextLease<T> Lease<T>() where T : class
            {
                var ctx = Build(options, currentTenant);
                return new ContextLease<T>((T)(object)ctx, ctx);
            }

            private sealed class ContextLease<T> : Shared.DependencyInjection.IContextLease<T> where T : class
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

        /// <summary>Der Mandant einer Maschine steht fest - sie hat kein Mandantensegment in der Route.</summary>
        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string scope;

            public FixedScope(string scope) => this.scope = scope;

            protected override string GetPermissionScopePrefix() => scope;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException("Der Mandant einer Maschine wechselt nicht.");
        }

        /// <summary>Kein angemeldeter Mensch - genau der Zustand, um den es hier geht.</summary>
        private sealed class NoUser : IContextUserProvider
        {
            public ClaimsPrincipal User { get; } = new(new ClaimsIdentity());

            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

            public string RequestPath => "/";

            public IServiceProvider Services => null;
        }

        /// <summary>
        /// Der echte Sicherheits-Kontext, nur mit der berechneten Spalte als gewoehnlicher gefuehrt: der
        /// InMemory-Provider rechnet <c>lower(TenantName)</c> nicht aus, und die Mandantenfilter fragen
        /// genau diese Spalte ab.
        /// </summary>
        public class TestSecurityContext : Basic.SecurityContext<TestSecurityContext>
        {
            public TestSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
                Microsoft.Extensions.Logging.ILogger<TestSecurityContext> logger,
                IOptions<DbContextModelBuilderOptions<TestSecurityContext>> modelBuilderOptions,
                DbContextOptions<TestSecurityContext> options)
                : base(tenantProvider, userProvider, logger, modelBuilderOptions, options)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<Tenant>().Property(t => t.TenantNameLower).ValueGeneratedNever();
            }
        }
    }
}
