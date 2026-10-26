using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using ITVComponents.EFRepo.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.SharedAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sec = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Basic.Models;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// "Eine Freigabe je Ort" am echten Adapter: <c>FindReusableSharedAsset</c> findet die bestehende
    /// anonyme Freigabe von Mandant + Vorlage + Pfad + Argumentwerten - und keine, die nur aehnlich ist.
    /// </summary>
    /// <remarks>
    /// Der Anlass ist der Laden-QR an der Kasse (MiniStore): jeder Druck auf "Teilen" stellte ein neues,
    /// unbefristetes Geheimnis aus. Jede Gegenprobe hier steht fuer einen Fall, in dem Wiederverwenden
    /// hiesse, jemandem den falschen Link zu geben - Kasse 2 den von Kasse 1, einem Empfaenger den eines
    /// anderen, einem Mandanten den eines fremden.
    /// <para>
    /// Echter Kontext auf InMemory, aus denselben Gruenden wie in <see cref="ClientAppMachineAccessTest"/>.
    /// Uebersetzungsfehler der Abfrage fallen hier nicht auf.
    /// </para>
    /// </remarks>
    [TestClass]
    public class SharedAssetReuseTest
    {
        private const string Acme = "acme";
        private const string Other = "other";
        private const string KioskKey = "pos-kiosk";
        private const string KioskPath = "/checkout/kiosk";

        private string dbName;

        [TestInitialize]
        public void Setup() => dbName = $"assetReuse_{Guid.NewGuid():N}";

        // --- Der Fall ---------------------------------------------------------------------------

        [TestMethod]
        public void TheStoredAnonymousShareOfThisPlace_IsFound_WithItsSecret()
        {
            var world = new World(dbName);
            var key = world.AddShare(Acme, clientAppId: 1);

            var found = world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier());

            Assert.IsNotNull(found);
            Assert.AreEqual(key, found.AssetKey);
            Assert.IsFalse(string.IsNullOrEmpty(found.AnonymousAccessTokenRaw),
                "ohne das Geheimnis laesst sich der anonyme Link nicht bauen");
        }

        [TestMethod]
        public void TheArgumentsAreCompared_InTheirCanonicalForm()
        {
            var world = new World(dbName);
            var key = world.AddShare(Acme, clientAppId: 7);

            var found = world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk,
                new Dictionary<string, string> { ["CLIENTAPPID"] = " 007 " }, Cashier());

            Assert.AreEqual(key, found?.AssetKey, "\"007\" und 7 bezeichnen dieselbe Kasse");
        }

        [TestMethod]
        public void OfTwoMatchingShares_TheNewestWins_AndTheOlderStaysValid()
        {
            var world = new World(dbName);
            var older = world.AddShare(Acme, clientAppId: 1);
            var newer = world.AddShare(Acme, clientAppId: 1);

            var found = world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier());

            Assert.AreEqual(newer, found?.AssetKey);
            Assert.IsTrue(world.Exists(older), "die aeltere haengt womoeglich gedruckt an der Tuer - nie still entwerten");
        }

        // --- Die Gegenproben --------------------------------------------------------------------

        [TestMethod]
        public void AnotherTill_GetsNothing()
        {
            var world = new World(dbName);
            world.AddShare(Acme, clientAppId: 1);

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(2), Cashier()),
                "Kasse 2 bekaeme sonst den Link von Kasse 1");
        }

        [TestMethod]
        public void TheShareOfAnotherTenant_IsNeverFound()
        {
            var world = new World(dbName);
            world.AddShare(Other, clientAppId: 1);

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier()));
        }

        [TestMethod]
        public void AShareWithRecipients_IsNotTheSamePlace()
        {
            var world = new World(dbName);
            world.AddShare(Acme, clientAppId: 1, extraUser: "someone@example.com");
            world.AddShare(Acme, clientAppId: 1, tenantFilter: Other);

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier()),
                "wer mit Empfaengern teilt, meint beim zweiten Mal einen anderen");
        }

        [TestMethod]
        public void AShareThatNeedsASignIn_IsNotReused()
        {
            var world = new World(dbName);
            world.AddShare(Acme, clientAppId: 1, anonymous: false, extraUser: "%");

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier()));
        }

        [TestMethod]
        public void AnExpiredShare_IsNotReused()
        {
            var world = new World(dbName);
            world.AddShare(Acme, clientAppId: 1, notAfter: DateTime.UtcNow.Date.AddDays(-1));
            world.AddShare(Acme, clientAppId: 1, notBefore: DateTime.UtcNow.Date.AddDays(1));

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier()),
                "ein QR, der nicht (mehr) gilt, ist ein toter Aushang");
        }

        [TestMethod]
        public void AShareOnAnotherPath_IsNotTheSamePlace()
        {
            var world = new World(dbName);
            world.AddShare(Acme, clientAppId: 1, rootPath: "/checkout/kiosk/old");

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier()));
        }

        [TestMethod]
        public void WithoutTheSwitch_NothingIsReused()
        {
            var world = new World(dbName, reuse: false);
            world.AddShare(Acme, clientAppId: 1);

            Assert.IsNull(world.Provider(Acme).FindReusableSharedAsset(KioskPath, Kiosk, Args(1), Cashier()),
                "bestehende Vorlagen stellen weiterhin bei jedem Teilen neu aus");
        }

        // --- Die Mandantengrenze des Besitzerrechts --------------------------------------------

        [TestMethod]
        public void OwnerAccess_IsLimitedToTheActiveTenant()
        {
            var world = new World(dbName);
            var foreign = world.AddShare(Other, clientAppId: 1);
            var own = world.AddShare(Acme, clientAppId: 1);

            var provider = world.Provider(Acme);

            Assert.IsInstanceOfType<FullAssetInfo>(provider.GetAssetInfo(own, Cashier(), asOwner: true));
            Assert.IsNotInstanceOfType<FullAssetInfo>(provider.GetAssetInfo(foreign, Cashier(), asOwner: true),
                "das Recht der Vorlage gilt im aktiven Mandanten - sonst verriete GetLinkAsync das Geheimnis " +
                "einer fremden Freigabe jedem, der ihren Schluessel kennt");
        }

        // --- Hilfen ------------------------------------------------------------------------------

        private static readonly AssetTemplateInfo Kiosk = new() { TemplateKey = KioskKey };

        private static Dictionary<string, string> Args(int clientAppId)
            => new() { ["clientAppId"] = clientAppId.ToString() };

        private static ClaimsPrincipal Cashier()
            => new(new ClaimsIdentity(new[] { new Claim(System.Security.Claims.ClaimTypes.Name, "cashier") }, "Test"));

        private sealed class World
        {
            private readonly DbContextOptions<ReuseSecurityContext> options;
            private readonly Dictionary<string, int> tenants = new();
            private readonly int templateId;

            public World(string dbName, bool reuse = true)
            {
                options = new DbContextOptionsBuilder<ReuseSecurityContext>().UseInMemoryDatabase(dbName).Options;
                using var ctx = New(Acme);
                foreach (var name in new[] { Acme, Other })
                {
                    // TenantNameLower von Hand - siehe ReuseSecurityContext.
                    var tenant = new Tenant { TenantName = name, DisplayName = name, TenantNameLower = name };
                    ctx.Tenants.Add(tenant);
                    ctx.SaveChanges();
                    tenants[name] = tenant.TenantId;
                }

                var template = new Sec.AssetTemplate
                {
                    Name = "Self-checkout station", SystemKey = KioskKey, ReuseStoredShare = reuse
                };
                ctx.AssetTemplates.Add(template);
                ctx.SaveChanges();
                templateId = template.AssetTemplateId;
                ctx.AssetTemplatePathFilters.Add(new Sec.AssetTemplatePath
                {
                    AssetTemplateId = templateId, PathTemplate = "^/checkout/kiosk$"
                });
                ctx.AssetTemplateArguments.Add(new AssetTemplateArgument
                {
                    AssetTemplateId = templateId, ArgumentName = "clientAppId", ArgumentType = AssetArgumentType.Int,
                    Required = true, SortOrder = 1
                });
                ctx.SaveChanges();
            }

            public string AddShare(string tenant, int clientAppId, bool anonymous = true, string extraUser = null,
                string tenantFilter = null, DateTime? notBefore = null, DateTime? notAfter = null,
                string rootPath = KioskPath)
            {
                using var ctx = New(tenant);
                var asset = new Sec.SharedAsset
                {
                    AssetKey = Guid.NewGuid().ToString("N"),
                    AnonymousAccessTokenRaw = Guid.NewGuid().ToString("B"),
                    AssetTemplateId = templateId,
                    AssetTitle = "Station",
                    TenantId = tenants[tenant],
                    RootPath = rootPath,
                    NotBefore = notBefore,
                    NotAfter = notAfter,
                    ArgumentValuesJson = $"{{\"clientAppId\":\"{clientAppId}\"}}"
                };
                ctx.SharedAssets.Add(asset);
                if (anonymous)
                {
                    ctx.SharedAssetUserFilters.Add(new Sec.SharedAssetUserFilter
                    {
                        Asset = asset, LabelFilter = "##ANONYMOUS"
                    });
                }

                if (extraUser != null)
                {
                    ctx.SharedAssetUserFilters.Add(new Sec.SharedAssetUserFilter { Asset = asset, LabelFilter = extraUser });
                }

                if (tenantFilter != null)
                {
                    ctx.SharedAssetTenantFilters.Add(new Sec.SharedAssetTenantFilter
                    {
                        Asset = asset, LabelFilter = tenantFilter
                    });
                }

                ctx.SaveChanges();
                return asset.AssetKey;
            }

            public bool Exists(string assetKey)
            {
                using var ctx = New(Acme);
                return ctx.SharedAssets.Any(n => n.AssetKey == assetKey);
            }

            public ISharedAssetAdapter Provider(string currentTenant)
            {
                var factory = new Factory(options, currentTenant);
                var repository = new Basic.Security.DbSecurityRepository<ReuseSecurityContext>(factory,
                    NullLogger<Basic.Security.DbSecurityRepository<ReuseSecurityContext>>.Instance);
                return new Basic.Security.SharedAssets.SharedAssetProvider<ReuseSecurityContext>(new Mapper(),
                    repository, factory, null, new NoServices());
            }

            private ReuseSecurityContext New(string tenant) => Factory.Build(options, tenant);
        }

        private sealed class Factory : IToolkitContextFactory
        {
            private readonly DbContextOptions<ReuseSecurityContext> options;
            private readonly string tenant;

            public Factory(DbContextOptions<ReuseSecurityContext> options, string tenant)
            {
                this.options = options;
                this.tenant = tenant;
            }

            public static ReuseSecurityContext Build(DbContextOptions<ReuseSecurityContext> options, string tenant)
                => new(new FixedScope(tenant), new NoUser(), NullLogger<ReuseSecurityContext>.Instance,
                    Microsoft.Extensions.Options.Options.Create(new DbContextModelBuilderOptions<ReuseSecurityContext>()), options);

            public T Create<T>() where T : class => (T)(object)Build(options, tenant);

            public IContextLease<T> Lease<T>() where T : class => new Lease<T>(Build(options, tenant));
        }

        private sealed class Lease<T> : IContextLease<T> where T : class
        {
            private readonly ReuseSecurityContext owner;

            public Lease(ReuseSecurityContext owner) => this.owner = owner;

            public T Context => (T)(object)owner;

            public void Dispose() => owner.Dispose();
        }

        private sealed class FixedScope : PermissionScopeBase
        {
            private readonly string scope;

            public FixedScope(string scope) => this.scope = scope;

            protected override string GetPermissionScopePrefix() => scope;

            protected override void SetPermissionScopePrefix(string newScope, bool asTemporary)
                => throw new NotSupportedException("The tenant of this test does not change.");
        }

        private sealed class NoUser : IContextUserProvider
        {
            public ClaimsPrincipal User { get; } = new(new ClaimsIdentity());

            public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>();

            public string RequestPath => "/";

            public IServiceProvider Services => null;
        }

        /// <summary>
        /// Kein Asset-Abschnitt, kein Mandant im Pfad, keine Gueltigkeitsregel - die Vorlage dieser Tests
        /// verlangt weder Feature noch Recht, also fragt der Adapter auch nicht danach.
        /// </summary>
        private sealed class NoServices : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }

        private sealed class Mapper : IUserNameMapper
        {
            public string[] GetUserLabels(IIdentity user) => new[] { user.Name ?? "unknown" };

            public string UniqueName { get; set; }

            public void Dispose()
            {
            }

            public event EventHandler Disposed { add { } remove { } }
        }

        /// <summary>
        /// Der echte Sicherheits-Kontext, die berechnete Spalte als gewoehnliche - InMemory rechnet
        /// <c>lower(TenantName)</c> nicht aus, und der Mandant des Kontexts wird ueber sie gesucht.
        /// </summary>
        public class ReuseSecurityContext : Basic.SecurityContext<ReuseSecurityContext>
        {
            public ReuseSecurityContext(IPermissionScope tenantProvider, IContextUserProvider userProvider,
                Microsoft.Extensions.Logging.ILogger<ReuseSecurityContext> logger,
                IOptions<DbContextModelBuilderOptions<ReuseSecurityContext>> modelBuilderOptions,
                DbContextOptions<ReuseSecurityContext> options)
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
