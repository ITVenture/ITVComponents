using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.DevicePairing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sec = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;
using TestSecurityContext = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test.ClientAppMachineAccessTest.TestSecurityContext;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// Die Kopplung, bei der das WEB die Anwendung waehlt: das Geraet sagt nur, was es ist (Typ-Schluessel des
    /// Templates), und wer bestaetigt, haengt es an eine Anwendung seines Mandanten - hoechstens eine aktive
    /// Instanz je Anwendung.
    /// </summary>
    /// <remarks>
    /// Auf InMemory, wie die uebrigen Tests am echten Sicherheits-Kontext (siehe ClientAppMachineAccessTest).
    /// Ein Uebersetzungsfehler der Abfragen faellt hier darum nicht auf.
    /// </remarks>
    [TestClass]
    public class DevicePairingChoiceTest
    {
        private const string PosType = "pos-agent";

        private World world = null!;

        [TestInitialize]
        public void Setup() => world = new World($"pairing_{Guid.NewGuid():N}");

        // --- Anlegen ---------------------------------------------------------------------------

        [TestMethod]
        public async Task AStartWithATypeKey_LeavesTheApplicationOpen()
        {
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            Assert.IsNotNull(request);
            using var ctx = world.New("acme");
            var pairing = ctx.DevicePairings.Single();
            Assert.IsNull(pairing.ClientAppId, "die Anwendung waehlt, wer bestaetigt - nicht das Geraet.");
            Assert.IsNull(pairing.TenantId, "ohne Anwendung gibt es noch keinen Mandanten.");
            Assert.AreEqual(world.PosTemplateId, pairing.ClientAppTemplateId);
        }

        [TestMethod]
        public async Task AnUnknownTypeKey_IsRefused()
        {
            Assert.IsNull(await world.Service("acme").StartAsync(null, "toaster", "PC-Laden-1"));
        }

        [TestMethod]
        public async Task AStartWithoutClientKeyAndTypeKey_IsRefused()
        {
            Assert.IsNull(await world.Service("acme").StartAsync(null, null, "PC-Laden-1"),
                "ein Vorgang ohne jede Zuordnung waere ein anonymer Eintrag ohne Zweck.");
        }

        // --- Die Auswahl -----------------------------------------------------------------------

        [TestMethod]
        public async Task TheChoice_IsTheApplicationsOfTheTemplateInTheConfirmersTenant()
        {
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var preview = await world.Service("acme").DescribeAsync(request.UserCode);

            Assert.IsTrue(preview.Found);
            Assert.IsNull(preview.ClientAppId);
            Assert.AreEqual("POS-Agent", preview.TemplateName);
            CollectionAssert.AreEquivalent(new[] { "Kasse 1", "Kasse 2" },
                preview.Candidates.Select(c => c.ClientAppName).ToArray(),
                "weder die Kasse des fremden Mandanten noch die Anwendung eines anderen Templates gehoert in die Auswahl.");
            CollectionAssert.AreEqual(new[] { "Kassieren" }, preview.TemplatePermissionSets.Select(s => s.Name).ToArray());
        }

        [TestMethod]
        public async Task AnApplicationWithAnotherInstance_IsOfferedButNotAvailable()
        {
            world.AddAccess(world.Kasse1, "PC-Laden-2");
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var preview = await world.Service("acme").DescribeAsync(request.UserCode);

            var kasse1 = preview.Candidates.Single(c => c.ClientAppId == world.Kasse1);
            Assert.IsFalse(kasse1.Available);
            Assert.AreEqual("PC-Laden-2", kasse1.ActiveInstance);
            Assert.IsTrue(preview.Candidates.Single(c => c.ClientAppId == world.Kasse2).Available);
        }

        // --- Bestaetigen -----------------------------------------------------------------------

        [TestMethod]
        public async Task ConfirmingWithoutAChoice_IsRefused()
        {
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var result = await world.Service("acme").ConfirmAsync(request.UserCode, null);

            Assert.IsFalse(result.Success);
            using var ctx = world.New("acme");
            Assert.AreEqual(0, ctx.ClientAppAccesses.Count());
        }

        [TestMethod]
        public async Task ConfirmingForAnApplication_BindsTheDeviceAndDeliversItsClientKey()
        {
            var service = world.Service("acme");
            var request = await service.StartAsync(null, PosType, "PC-Laden-1");

            var result = await service.ConfirmAsync(request.UserCode, world.Kasse1);
            var poll = await service.PollAsync(request.DeviceCode);

            Assert.IsTrue(result.Success, result.Reason);
            Assert.AreEqual(DevicePairingState.Delivered, poll.State);
            Assert.AreEqual("key-kasse-1", poll.ClientKey,
                "der Agent meldet sich mit dem ClientKey am Dienst-Verteiler an - er darf ihn nicht aus dem Schluessel kratzen muessen.");
            StringAssert.StartsWith(poll.ApiKey, "key-kasse-1.Kasse1-",
                "das Label traegt den Namen der ANWENDUNG (ohne Leerzeichen, ohne Punkt), nicht den des Rechners.");
            using var ctx = world.New("acme");
            var pairing = ctx.DevicePairings.Single();
            Assert.AreEqual(world.Kasse1, pairing.ClientAppId);
            Assert.AreEqual(world.AcmeId, pairing.TenantId);
            Assert.AreEqual("PC-Laden-1", ctx.ClientAppAccesses.Single().DeviceLabel);
        }

        [TestMethod]
        public async Task ASecondInstance_IsRefused()
        {
            world.AddAccess(world.Kasse1, "PC-Laden-2");
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var result = await world.Service("acme").ConfirmAsync(request.UserCode, world.Kasse1);

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Reason, "PC-Laden-2", "die Meldung nennt die Instanz, die im Weg steht.");
            using var ctx = world.New("acme");
            Assert.AreEqual(1, ctx.ClientAppAccesses.Count(n => n.RevokedUtc == null),
                "die laufende Kasse darf von einer Fehlbedienung nicht stillgelegt werden.");
        }

        [TestMethod]
        public async Task TheSameDeviceAgain_ReplacesItsAccess()
        {
            world.AddAccess(world.Kasse1, "PC-Laden-1");
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var result = await world.Service("acme").ConfirmAsync(request.UserCode, world.Kasse1);

            Assert.IsTrue(result.Success, result.Reason);
            using var ctx = world.New("acme");
            Assert.AreEqual(2, ctx.ClientAppAccesses.Count());
            Assert.AreEqual(1, ctx.ClientAppAccesses.Count(n => n.RevokedUtc == null),
                "der alte Schluessel desselben Geraets wird widerrufen, sonst meldet es sich damit weiter an.");
        }

        [TestMethod]
        public async Task AnApplicationOfAnotherTemplate_IsRefused()
        {
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var result = await world.Service("acme").ConfirmAsync(request.UserCode, world.Drucker);

            Assert.IsFalse(result.Success, "eine Kasse an der Anwendung eines Druckers waere eine Kasse ohne passende Rollen.");
        }

        [TestMethod]
        public async Task AnApplicationOfAnotherTenant_IsRefused()
        {
            var request = await world.Service("acme").StartAsync(null, PosType, "PC-Laden-1");

            var result = await world.Service("acme").ConfirmAsync(request.UserCode, world.Fremd);

            Assert.IsFalse(result.Success);
            using var ctx = world.New("acme");
            Assert.AreEqual(0, ctx.ClientAppAccesses.Count());
        }

        [TestMethod]
        public async Task ADeviceWithAClientKey_CannotBeRedirected()
        {
            var request = await world.Service("acme").StartAsync("key-kasse-1", null, "PC-Laden-1");

            var redirected = await world.Service("acme").ConfirmAsync(request.UserCode, world.Kasse2);
            var asked = await world.Service("acme").ConfirmAsync(request.UserCode, null);

            Assert.IsFalse(redirected.Success, "das Geraet hat seine Anwendung selbst festgelegt.");
            Assert.IsTrue(asked.Success, asked.Reason);
        }

        // --- Testgeruest ----------------------------------------------------------------------

        private sealed class World
        {
            private readonly DbContextOptions<TestSecurityContext> options;

            public World(string dbName)
            {
                options = new DbContextOptionsBuilder<TestSecurityContext>().UseInMemoryDatabase(dbName).Options;
                using var ctx = New("acme");
                var acme = AddTenant(ctx, "acme");
                var other = AddTenant(ctx, "other");
                AcmeId = acme.TenantId;

                var pos = new Sec.ClientAppTemplate { Name = "POS-Agent", TypeKey = PosType };
                var printer = new Sec.ClientAppTemplate { Name = "Drucker-Agent", TypeKey = "printer-agent" };
                ctx.ClientAppTemplates.AddRange(pos, printer);
                ctx.SaveChanges();
                PosTemplateId = pos.ClientAppTemplateId;

                var set = new Sec.AppPermissionSet { ClientAppTemplateId = pos.ClientAppTemplateId, Name = "Kassieren" };
                ctx.AppPermissionSets.Add(set);
                ctx.SaveChanges();

                Kasse1 = App(ctx, acme, pos, "Kasse 1", "key-kasse-1");
                Kasse2 = App(ctx, acme, pos, "Kasse 2", "key-kasse-2");
                Drucker = App(ctx, acme, printer, "Drucker", "key-drucker");
                Fremd = App(ctx, other, pos, "Kasse 1", "key-fremd");
                ctx.ClientAppPermissions.Add(new Sec.ClientAppPermission { ClientAppId = Kasse1, AppPermissionSetId = set.AppPermissionSetId });
                ctx.SaveChanges();
            }

            public int AcmeId { get; }
            public int PosTemplateId { get; }
            public int Kasse1 { get; }
            public int Kasse2 { get; }
            public int Drucker { get; }
            public int Fremd { get; }

            public DbDevicePairingService<TestSecurityContext, Tenant, int, Sec.User, Sec.Role, Sec.Permission, Sec.UserRole,
                Sec.RolePermission, Sec.TenantUser, Sec.RoleRole, Sec.GlobalRole, Sec.GlobalRolePermission, Sec.GRoleLRole,
                Sec.AppPermission, Sec.AppPermissionSet, Sec.ClientAppPermission, Sec.ClientApp, Sec.ClientAppAccess,
                Sec.DevicePairing, Sec.ClientAppTemplate> Service(string tenant)
                => new(new Factory(this, tenant), new Settings(), new FixedScope(tenant), new NoUser(), null!,
                    NullLoggerFactory.Instance);

            public void AddAccess(int clientAppId, string deviceLabel)
            {
                using var ctx = New("acme");
                ctx.ClientAppAccesses.Add(new Sec.ClientAppAccess
                {
                    ClientAppId = clientAppId,
                    Label = $"seed-{Guid.NewGuid():N}",
                    DeviceLabel = deviceLabel,
                    SecretHash = "not-verified-here",
                    CreatedUtc = DateTime.UtcNow
                });
                ctx.SaveChanges();
            }

            public TestSecurityContext New(string tenant)
                => new(new FixedScope(tenant), new NoUser(), NullLogger<TestSecurityContext>.Instance,
                    Microsoft.Extensions.Options.Options.Create(new DbContextModelBuilderOptions<TestSecurityContext>()), options);

            private static Tenant AddTenant(TestSecurityContext ctx, string name)
            {
                // TenantNameLower von Hand - im Test ist die berechnete Spalte eine gewoehnliche.
                var tenant = new Tenant { TenantName = name, DisplayName = name, TenantNameLower = name };
                ctx.Tenants.Add(tenant);
                ctx.SaveChanges();
                return tenant;
            }

            private static int App(TestSecurityContext ctx, Tenant tenant, Sec.ClientAppTemplate template, string name, string key)
            {
                var app = new Sec.ClientApp
                {
                    TenantId = tenant.TenantId, ClientAppTemplateId = template.ClientAppTemplateId, ClientName = name,
                    ClientKey = key, ClientSecret = "", Enabled = true, CreatedUtc = DateTime.UtcNow
                };
                ctx.ClientApps.Add(app);
                ctx.SaveChanges();
                return app.ClientAppId;
            }

            private sealed class Factory : IDbContextFactory<TestSecurityContext>
            {
                private readonly World world;
                private readonly string tenant;

                public Factory(World world, string tenant)
                {
                    this.world = world;
                    this.tenant = tenant;
                }

                public TestSecurityContext CreateDbContext() => world.New(tenant);
            }
        }

        private sealed class Settings : IGlobalSettings<DevicePairingOptions>
        {
            public DevicePairingOptions Value { get; } = new();
            public DevicePairingOptions ValueOrDefault => Value;
            public DevicePairingOptions GetValue(string explicitSettingName) => Value;
            public DevicePairingOptions GetValueOrDefault(string explicitSettingName) => Value;
        }

        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string scope;

            public FixedScope(string scope) => this.scope = scope;

            protected override string GetPermissionScopePrefix() => scope;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException();
        }

        /// <summary>Kein aufloesbarer Benutzer - die Bestaetigung wird dann ohne Bestaetiger protokolliert.</summary>
        private sealed class NoUser : IContextUserProvider
        {
            public ClaimsPrincipal User { get; } = new(new ClaimsIdentity());

            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

            public string RequestPath => "/";

            public IServiceProvider Services => null!;
        }
    }
}
