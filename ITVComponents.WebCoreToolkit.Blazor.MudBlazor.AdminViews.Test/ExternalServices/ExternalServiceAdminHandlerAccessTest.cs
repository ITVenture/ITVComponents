using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;
using ITVComponents.WebCoreToolkit.Blazor.Paging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tenant = ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Tenant;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.Test.ExternalServices
{
    /// <summary>
    /// Nagelt die Mandantengrenze des Blazor-<c>ExternalServiceAdminHandler</c> fest: Sysadmin erreicht alles, ein
    /// Mandanten-Admin liest die eigenen und die globalen Dienste und aendert nur die eigenen.
    /// </summary>
    /// <remarks>
    /// Der Handler arbeitet mit <c>ShowAllTenants</c> - der Mandantenfilter des Kontexts greift also NICHT, und
    /// <c>AccessibleServices</c> ist die einzige Grenze. Vorher pruefte er bei Bearbeiten, Loeschen, Details und Test
    /// nur die Id: ein Mandanten-Admin konnte per Id fremde und globale Dienste aendern (Leitfaden §84.3).
    /// <para>
    /// <b>Warum der echte Kontext und InMemory:</b> der Handler ist ueber die volle Schliessung des
    /// Sicherheitskontexts generisch, ein verkleinerter Kontext passt nicht hinein. Der InMemory-Provider wertet
    /// client-seitig aus - ein Uebersetzungsfehler der Abfragen faellt hier also nie auf, nur die Logik der Grenze.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ExternalServiceAdminHandlerAccessTest
    {
        private const string Own = "acme";
        private const string Foreign = "contoso";
        private const string View = "Services.Connections.View";
        private const string Write = "Services.Connections.Write";

        private static readonly ClaimsPrincipal Caller = new(new ClaimsIdentity());
        private static readonly ListQuery All = new() { PageSize = 100 };

        private World world = null!;

        [TestInitialize]
        public void Setup() => world = World.Seed($"externalServices_{Guid.NewGuid():N}");

        // --- Lesen ---------------------------------------------------------------------------

        [TestMethod]
        public async Task ATenantAdmin_ListsOwnAndGlobalServices_ButNotForeignOnes()
        {
            var handler = world.Handler(Own, Write);

            var names = (await handler.ListAsync(Caller, All)).Items.Select(n => n.UniqueConnectionName).ToArray();

            CollectionAssert.AreEquivalent(new[] { "own", "global" }, names);
        }

        [TestMethod]
        public async Task ATenantAdmin_GetsNoDetailsOfAForeignService()
        {
            var handler = world.Handler(Own, View);

            Assert.IsNull(await handler.GetDetailsAsync(Caller, world.ForeignServiceId));
            Assert.IsNotNull(await handler.GetDetailsAsync(Caller, world.GlobalServiceId), "Global services are readable.");
            var own = await handler.GetDetailsAsync(Caller, world.OwnServiceId);
            Assert.IsNotNull(own);
            CollectionAssert.AreEqual(new[] { "client_id" }, own!.TokenRequestParameters!.Select(n => n.Key).ToArray(),
                "The details must carry the parameters - they are loaded explicitly on an untracked entity.");
        }

        [TestMethod]
        public async Task ATenantAdmin_SeesNoParametersOfAForeignService()
        {
            var handler = world.Handler(Own, View);

            Assert.AreEqual(0, (await handler.ListParametersAsync(Caller, world.ForeignServiceId, All)).TotalCount);
            Assert.AreEqual(1, (await handler.ListParametersAsync(Caller, world.OwnServiceId, All)).TotalCount);
        }

        [TestMethod]
        public async Task WithoutATenant_NothingIsVisible()
        {
            // "TenantId == currentTenantId" mit null wuerde sonst genau die globalen Dienste treffen
            var handler = world.Handler(null, Write);

            Assert.AreEqual(0, (await handler.ListAsync(Caller, All)).TotalCount);
            Assert.IsNull(await handler.GetDetailsAsync(Caller, world.GlobalServiceId));
        }

        // --- Schreiben -----------------------------------------------------------------------

        [TestMethod]
        public async Task ATenantAdmin_CanNeitherUpdateNorDeleteForeignOrGlobalServices()
        {
            var handler = world.Handler(Own, Write);

            foreach (var id in new[] { world.ForeignServiceId, world.GlobalServiceId })
            {
                Assert.IsNull(await handler.UpdateAsync(Caller, Rename(id, "hijacked")), $"Service {id} was updated.");
                Assert.IsFalse(await handler.DeleteAsync(Caller, id), $"Service {id} was deleted.");
            }

            using var db = world.Inspect();
            Assert.AreEqual(3, db.ExternalOAuthServices.Count());
            Assert.IsFalse(db.ExternalOAuthServices.Any(n => n.UniqueConnectionName == "hijacked"));
        }

        [TestMethod]
        public async Task ATenantAdmin_UpdatesAndDeletesTheOwnService()
        {
            var handler = world.Handler(Own, Write);

            Assert.IsNotNull(await handler.UpdateAsync(Caller, Rename(world.OwnServiceId, "renamed")));
            using (var db = world.Inspect())
            {
                Assert.AreEqual("renamed", db.ExternalOAuthServices.Single(n => n.OAuthServiceId == world.OwnServiceId).UniqueConnectionName);
            }

            Assert.IsTrue(await handler.DeleteAsync(Caller, world.OwnServiceId));
        }

        [TestMethod]
        public void TheTenantFilter_LetsGlobalServicesThrough_ButNoForeignOnes()
        {
            // Der Laufzeitweg (GetExternalService, GetBufferedToken) liest ohne Trust-Override - der Filter allein
            // entscheidet, ob ein Mandant einen globalen Dienst (die App der Plattform) findet.
            using var db = world.SignedInContext(Own);
            Assert.IsTrue(db.FilterAvailable, "Without a signed-in user the filter does not apply - the test would prove nothing.");

            var visible = db.ExternalOAuthServices.Select(n => n.UniqueConnectionName).OrderBy(n => n).ToArray();

            Assert.AreEqual("global,own", string.Join(",", visible));
        }

        [TestMethod]
        public async Task ASecretParameter_IsStoredEncryptedAndNeverReadBack()
        {
            var handler = world.Handler(Own, Write);

            var result = await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceId = world.OwnServiceId, ParameterName = "api_key", ParameterValue = "s3cret", IsSecret = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNull(result.Parameter!.ParameterValue, "A secret must not come back to the view.");
            Assert.IsTrue(result.Parameter.HasValue);
            using (var db = world.Inspect())
            {
                var stored = db.ExternalOAuthServices.Include(n => n.TokenParameters)
                    .Single(n => n.OAuthServiceId == world.OwnServiceId).TokenParameters.Single(n => n.ParameterName == "api_key");
                Assert.IsTrue(stored.IsSecret);
                Assert.AreEqual($"enc[{Own}]:s3cret", stored.ParameterValue, "Encrypted with the key of the service's tenant.");
            }

            var listed = await handler.ListParametersAsync(Caller, world.OwnServiceId, All);
            var secret = listed.Items.Single(n => n.ParameterName == "api_key");
            Assert.IsNull(secret.ParameterValue);
            Assert.AreEqual("a", listed.Items.Single(n => n.ParameterName == "client_id").ParameterValue,
                "A parameter that is not secret stays readable.");
        }

        [TestMethod]
        public async Task ASecretParameter_KeepsItsValueWhenSavedEmpty_AndIsNotSilentlyMadePlain()
        {
            var handler = world.Handler(Own, Write);
            var saved = (await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceId = world.OwnServiceId, ParameterName = "password", ParameterValue = "pw", IsSecret = true
            })).Parameter!;

            // Name aendern, Wert leer: das Geheimnis bleibt.
            var renamed = await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceParameterId = saved.OAuthServiceParameterId, OAuthServiceId = world.OwnServiceId,
                ParameterName = "pass", ParameterValue = null, IsSecret = true
            });
            Assert.IsTrue(renamed.Success, renamed.Error);

            // Auf Klartext ohne neuen Wert: abgelehnt.
            var plain = await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceParameterId = saved.OAuthServiceParameterId, OAuthServiceId = world.OwnServiceId,
                ParameterName = "pass", ParameterValue = null, IsSecret = false
            });
            Assert.IsFalse(plain.Success);

            using var db = world.Inspect();
            var stored = db.ExternalOAuthServices.Include(n => n.TokenParameters)
                .Single(n => n.OAuthServiceId == world.OwnServiceId).TokenParameters.Single(n => n.ParameterName == "pass");
            Assert.IsTrue(stored.IsSecret);
            Assert.AreEqual($"enc[{Own}]:pw", stored.ParameterValue);
        }

        [TestMethod]
        public async Task MarkingAPlainParameterSecret_EncryptsTheExistingValue()
        {
            var handler = world.Handler(Own, Write);
            var existing = (await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceId = world.OwnServiceId, ParameterName = "organization", ParameterValue = "acme"
            })).Parameter!;
            Assert.AreEqual("acme", existing.ParameterValue);

            var result = await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceParameterId = existing.OAuthServiceParameterId, OAuthServiceId = world.OwnServiceId,
                ParameterName = "organization", ParameterValue = null, IsSecret = true
            });

            Assert.IsTrue(result.Success, result.Error);
            using var db = world.Inspect();
            Assert.AreEqual($"enc[{Own}]:acme", db.ExternalOAuthServices.Include(n => n.TokenParameters)
                .Single(n => n.OAuthServiceId == world.OwnServiceId).TokenParameters
                .Single(n => n.ParameterName == "organization").ParameterValue);
        }

        [TestMethod]
        public void DecryptingTheConnection_LeavesTheBufferedArrayAlone()
        {
            var shared = new[]
            {
                new KeyValuePair<string, string>("audience", "api"),
                new KeyValuePair<string, string>("client_secret", "cipher")
            };
            var connection = new ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect.ExternalServiceConnection
            {
                TokenRequestParameters = shared,
                SecretTokenRequestParameters = new[] { "client_secret" }
            };

            connection.DecryptTokenRequestParameters(v => v == "cipher" ? "plain" : throw new InvalidOperationException(v));

            Assert.AreEqual("plain", connection.TokenRequestParameters.Single(n => n.Key == "client_secret").Value);
            Assert.AreEqual("api", connection.TokenRequestParameters.Single(n => n.Key == "audience").Value);
            Assert.AreEqual("cipher", shared[1].Value, "The shared (buffered) array must not receive plain text.");
        }

        [TestMethod]
        public async Task ATenantAdmin_CanNotTouchTheParametersOfForeignOrGlobalServices()
        {
            var handler = world.Handler(Own, Write);

            foreach (var id in new[] { world.ForeignServiceId, world.GlobalServiceId })
            {
                var result = await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
                {
                    OAuthServiceId = id, ParameterName = "organization", ParameterValue = "evil"
                });
                Assert.IsFalse(result.Success, $"A parameter was added to service {id}.");
            }

            Assert.IsFalse(await handler.DeleteParameterAsync(Caller, world.ForeignServiceId, world.ForeignParameterId));

            using var db = world.Inspect();
            var foreign = db.ExternalOAuthServices.Include(n => n.TokenParameters).Single(n => n.OAuthServiceId == world.ForeignServiceId);
            Assert.AreEqual(1, foreign.TokenParameters.Count, "The parameter of the foreign service was deleted.");
            Assert.IsFalse(db.ExternalOAuthServices.Include(n => n.TokenParameters)
                .Single(n => n.OAuthServiceId == world.GlobalServiceId).TokenParameters.Any());
        }

        [TestMethod]
        public async Task ATenantAdmin_CreatesForTheOwnTenant_WhateverTheInputSays()
        {
            var handler = world.Handler(Own, Write);

            var created = await handler.CreateAsync(Caller, new ExternalOAuthServiceViewModel
            {
                UniqueConnectionName = "smuggled", Global = true, TenantId = world.ForeignTenantId,
                AuthenticationType = ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
            });

            Assert.IsNotNull(created);
            using var db = world.Inspect();
            var row = db.ExternalOAuthServices.Single(n => n.UniqueConnectionName == "smuggled");
            Assert.IsFalse(row.Global);
            Assert.AreEqual(world.OwnTenantId, row.TenantId);
        }

        [TestMethod]
        public async Task ASysadmin_ReachesEveryService()
        {
            var handler = world.Handler(Own, Write, "Sysadmin");

            Assert.AreEqual(3, (await handler.ListAsync(Caller, All)).TotalCount);
            Assert.IsNotNull(await handler.UpdateAsync(Caller, Rename(world.ForeignServiceId, "by-sysadmin")));
            Assert.IsTrue((await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceId = world.GlobalServiceId, ParameterName = "organization", ParameterValue = "itv"
            })).Success);
        }

        // --- Logins eines globalen Dienstes ----------------------------------------------------

        [TestMethod]
        public async Task OnAGlobalService_ATenantAdminSeesAndRevokesOnlyTheOwnLogin()
        {
            var handler = world.Handler(Own, Write);

            var logins = (await handler.ListLoginsAsync(Caller, world.GlobalServiceId, All)).Items;
            CollectionAssert.AreEqual(new[] { world.OwnTenantId }, logins.Select(n => n.TenantId).ToArray());

            Assert.IsFalse(await handler.RevokeLoginAsync(Caller, world.ForeignLoginId));
            Assert.IsTrue(await handler.RevokeLoginAsync(Caller, world.OwnLoginId));

            using var db = world.Inspect();
            Assert.IsFalse(db.ExternalOAuthServiceTenantLogins.Single(n => n.ExternalOAuthServiceTenantLoginId == world.ForeignLoginId).Revoked);
        }

        // --- Rechte ----------------------------------------------------------------------------

        [TestMethod]
        public async Task ReadingDoesNotPermitWriting()
        {
            var handler = world.Handler(Own, View);

            Assert.IsNull(await handler.UpdateAsync(Caller, Rename(world.OwnServiceId, "renamed")));
            Assert.IsFalse(await handler.DeleteAsync(Caller, world.OwnServiceId));
            Assert.IsFalse((await handler.SaveParameterAsync(Caller, new ExternalOAuthServiceParameterViewModel
            {
                OAuthServiceId = world.OwnServiceId, ParameterName = "organization"
            })).Success);
        }

        private static ExternalOAuthServiceViewModel Rename(int id, string name) => new()
        {
            OAuthServiceId = id, UniqueConnectionName = name,
            AuthenticationType = ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
        };

        /// <summary>
        /// Zwei Mandanten mit je einem Dienst, dazu ein globaler; der eigene und der fremde Dienst tragen je einen
        /// Token-Parameter, der globale hat einen Login je Mandant.
        /// </summary>
        private sealed class World
        {
            private readonly string databaseName;

            private World(string databaseName) => this.databaseName = databaseName;

            public int OwnTenantId { get; private set; }
            public int ForeignTenantId { get; private set; }
            public int OwnServiceId { get; private set; }
            public int ForeignServiceId { get; private set; }
            public int GlobalServiceId { get; private set; }
            public int ForeignParameterId { get; private set; }
            public int OwnLoginId { get; private set; }
            public int ForeignLoginId { get; private set; }

            public ExternalServiceTestContext Context(string? tenant) => ExternalServiceTestContext.Build(databaseName, tenant);

            /// <summary>Ein Kontext mit den Filtern der Anwendung und angemeldetem Benutzer - so liest die Laufzeit.</summary>
            public ExternalServiceTestContext SignedInContext(string tenant) => ExternalServiceTestContext.BuildFiltered(databaseName, tenant);

            /// <summary>Ein Kontext ohne Mandantenfilter - zum Anlegen und Nachsehen, nicht fuer den Handler.</summary>
            public ExternalServiceTestContext Inspect()
            {
                var db = Context(null);
                db.ShowAllTenants = true;
                db.HideGlobals = false;
                return db;
            }

            public ExternalServiceTestHandler Handler(string? tenant, params string[] permissions)
            {
                var services = PermissionEnvironment.WithPermissions(permissions);
                return new ExternalServiceTestHandler(new Factory(this, tenant), services,
                    (ITVComponents.WebCoreToolkit.Security.ISecurityRepository)services.GetService(typeof(ITVComponents.WebCoreToolkit.Security.ISecurityRepository))!);
            }

            public static World Seed(string databaseName)
            {
                var world = new World(databaseName);
                using var db = world.Inspect();
                // TenantNameLower von Hand (in der Datenbank berechnet); den Dienstnamen rechnet der Testkontext beim Speichern
                var own = new Tenant { TenantName = Own, DisplayName = Own, TenantNameLower = Own };
                var foreign = new Tenant { TenantName = Foreign, DisplayName = Foreign, TenantNameLower = Foreign };
                db.Tenants.AddRange(own, foreign);
                db.SaveChanges();
                world.OwnTenantId = own.TenantId;
                world.ForeignTenantId = foreign.TenantId;

                var ownService = Service("own", own.TenantId);
                ownService.TokenParameters.Add(new ExternalOAuthServiceParameter { ParameterName = "client_id", ParameterValue = "a" });
                var foreignService = Service("foreign", foreign.TenantId);
                var foreignParameter = new ExternalOAuthServiceParameter { ParameterName = "client_id", ParameterValue = "b" };
                foreignService.TokenParameters.Add(foreignParameter);
                var globalService = Service("global", null);
                db.ExternalOAuthServices.AddRange(ownService, foreignService, globalService);
                db.SaveChanges();
                world.OwnServiceId = ownService.OAuthServiceId;
                world.ForeignServiceId = foreignService.OAuthServiceId;
                world.GlobalServiceId = globalService.OAuthServiceId;
                world.ForeignParameterId = foreignParameter.OAuthServiceParameterId;

                var ownLogin = new FlatExternalOAuthServiceTenantLogin { OAuthServiceId = globalService.OAuthServiceId, TenantId = own.TenantId, Token = "t1" };
                var foreignLogin = new FlatExternalOAuthServiceTenantLogin { OAuthServiceId = globalService.OAuthServiceId, TenantId = foreign.TenantId, Token = "t2" };
                db.ExternalOAuthServiceTenantLogins.AddRange(ownLogin, foreignLogin);
                db.SaveChanges();
                world.OwnLoginId = ownLogin.ExternalOAuthServiceTenantLoginId;
                world.ForeignLoginId = foreignLogin.ExternalOAuthServiceTenantLoginId;
                return world;
            }

            private static FlatExternalOAuthService Service(string name, int? tenantId) => new()
            {
                UniqueConnectionName = name,
                AuthorizationEndpoint = string.Empty, TokenEndpoint = "https://token.invalid", RevocationEndpoint = string.Empty,
                ClientId = "client", ClientSecret = string.Empty, Scope = string.Empty,
                Global = tenantId == null, TenantId = tenantId,
                AuthenticationType = ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
            };

            /// <summary>Ein frischer Kontext je Vorgang, wie die echte Fabrik; alle teilen die benannte InMemory-Ablage.</summary>
            private sealed class Factory : IDbContextFactory<ExternalServiceTestContext>
            {
                private readonly World owner;
                private readonly string? tenant;

                public Factory(World owner, string? tenant)
                {
                    this.owner = owner;
                    this.tenant = tenant;
                }

                public ExternalServiceTestContext CreateDbContext() => owner.Context(tenant);
            }
        }
    }
}
