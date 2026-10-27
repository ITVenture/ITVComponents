using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.FlatTenantModels;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.Billing.Test
{
    /// <summary>
    /// Der Speicher der eigenen Anbieter-Zugaenge: Geheimnisse liegen in der Zugangsdaten-Zeile GENAU des Mandanten, nicht
    /// an der Definition, und nichts wird geerbt.
    /// </summary>
    [TestClass]
    public class ProviderCredentialStoreTests
    {
        private const string Acme = "acme";
        private const string Contoso = "contoso";

        [TestMethod]
        public async Task SaveSecret_PutsTheSecretIntoTheTenantsRow_NotTheDefinition()
        {
            var (store, factory, acme, _) = await Build();

            await store.SaveSecretAsync(acme, "payrexx-acme", ExternalServiceAuthenticationType.ApiKey, "X-API-KEY", "k1");

            await using var db = await factory.CreateDbContextAsync();
            var service = await db.Services.SingleAsync();
            Assert.AreEqual("X-API-KEY", service.ClientId, "The header name belongs to the definition.");
            Assert.AreEqual(string.Empty, service.ClientSecret, "The secret must not stay on the definition.");
            var row = await db.Logins.SingleAsync();
            Assert.AreEqual(acme, row.TenantId);
            Assert.IsFalse(row.Inheritable, "Payment credentials are never passed on.");
            StringAssert.StartsWith(row.CredentialJson, $"enc[{Acme}]:", "Encrypted with the tenant's key.");

            var credential = await store.GetCredentialAsync(acme, "payrexx-acme");
            Assert.AreEqual("X-API-KEY", credential.ClientId);
            Assert.AreEqual("k1", credential.Secret);
        }

        [TestMethod]
        public async Task Basic_KeepsTheUserWithTheCredentials()
        {
            var (store, _, acme, _) = await Build();

            await store.SaveSecretAsync(acme, "wallee-acme", ExternalServiceAuthenticationType.Basic, "user-17", "pw");

            var credential = await store.GetCredentialAsync(acme, "wallee-acme");
            Assert.AreEqual("user-17", credential.ClientId);
            Assert.AreEqual("pw", credential.Secret);
        }

        [TestMethod]
        public async Task ASecretOnTheDefinition_StillAnswers_UntilItIsMoved()
        {
            var (store, factory, acme, _) = await Build();
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.Services.Add(Service("sumup-acme", acme, ExternalServiceAuthenticationType.BearerToken, $"enc[{Acme}]:legacy"));
                await db.SaveChangesAsync();
            }

            Assert.AreEqual("legacy", (await store.GetCredentialAsync(acme, "sumup-acme")).Secret);
        }

        [TestMethod]
        public async Task AnotherTenantsRow_IsNeverUsed()
        {
            var (store, factory, acme, contoso) = await Build();
            await store.SaveSecretAsync(contoso, "payrexx", ExternalServiceAuthenticationType.ApiKey, "X-API-KEY", "theirs");
            await using (var db = await factory.CreateDbContextAsync())
            {
                // Ein Dienst gleichen Namens beim eigenen Mandanten, aber ohne eigene Zeile.
                db.Services.Add(Service("payrexx", acme, ExternalServiceAuthenticationType.ApiKey, string.Empty));
                await db.SaveChangesAsync();
            }

            var ex = await Assert.ThrowsExactlyAsync<TenantPaymentException>(() => store.GetCredentialAsync(acme, "payrexx"));
            Assert.AreEqual(PaymentErrorCodes.OwnAccessUnavailable, ex.Code);
        }

        private static FlatExternalOAuthService Service(string name, int tenantId, ExternalServiceAuthenticationType type, string secret) => new()
        {
            UniqueConnectionName = name, CalculatedUniqueServiceName = $"T{tenantId}-{name}", TenantId = tenantId,
            AuthorizationEndpoint = string.Empty, TokenEndpoint = string.Empty, RevocationEndpoint = string.Empty,
            ClientId = "X-API-KEY", ClientSecret = secret, Scope = string.Empty, AuthenticationType = type
        };

        private static async Task<(ExternalServiceProviderCredentialStore<StoreTestContext, Tenant, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin> Store,
            IDbContextFactory<StoreTestContext> Factory, int Acme, int Contoso)> Build()
        {
            IDbContextFactory<StoreTestContext> factory = new StoreTestContextFactory(Guid.NewGuid().ToString());
            int acme, contoso;
            await using (var db = await factory.CreateDbContextAsync())
            {
                var a = new Tenant { TenantName = Acme, DisplayName = Acme, TenantNameLower = Acme };
                var c = new Tenant { TenantName = Contoso, DisplayName = Contoso, TenantNameLower = Contoso };
                db.Tenants.AddRange(a, c);
                await db.SaveChangesAsync();
                acme = a.TenantId;
                contoso = c.TenantId;
            }

            var store = new ExternalServiceProviderCredentialStore<StoreTestContext, Tenant, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin>(
                factory, CipherOnly.Create(), null!,
                NullLogger<ExternalServiceProviderCredentialStore<StoreTestContext, Tenant, FlatExternalOAuthService, FlatExternalOAuthServiceState, FlatExternalOAuthServiceTenantLogin>>.Instance);
            return (store, factory, acme, contoso);
        }

        /// <summary>Der kleinste Kontext mit den drei Tabellen, die der Speicher liest.</summary>
        public class StoreTestContext : DbContext
        {
            public StoreTestContext(DbContextOptions<StoreTestContext> options) : base(options) { }

            public DbSet<Tenant> Tenants { get; set; } = null!;

            public DbSet<FlatExternalOAuthService> Services { get; set; } = null!;

            public DbSet<FlatExternalOAuthServiceTenantLogin> Logins { get; set; } = null!;

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                // Berechnete Spalten: InMemory rechnet sie nicht aus, der Test setzt sie selbst.
                modelBuilder.Entity<Tenant>().Property(t => t.TenantNameLower).ValueGeneratedNever();
                modelBuilder.Entity<FlatExternalOAuthService>().Property(s => s.CalculatedUniqueServiceName).ValueGeneratedNever();
            }

            /// <summary>
            /// Rechnet den eindeutigen Dienstnamen nach, wie es die Datenbank tut ('GLOBAL'+Name bzw. 'T{Id}-'+Name) - der
            /// Speicher legt Dienste selbst an, und InMemory kennt die berechnete Pflichtspalte nicht.
            /// </summary>
            public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, System.Threading.CancellationToken cancellationToken = default)
            {
                foreach (var added in ChangeTracker.Entries<FlatExternalOAuthService>().Where(e => e.State == EntityState.Added))
                {
                    var service = added.Entity;
                    service.CalculatedUniqueServiceName ??= service.TenantId == null
                        ? $"GLOBAL{service.UniqueConnectionName}"
                        : $"T{service.TenantId}-{service.UniqueConnectionName}";
                }

                return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }
        }

        private sealed class StoreTestContextFactory : IDbContextFactory<StoreTestContext>
        {
            private readonly string name;

            public StoreTestContextFactory(string name) => this.name = name;

            public StoreTestContext CreateDbContext()
                => new(new DbContextOptionsBuilder<StoreTestContext>().UseInMemoryDatabase(name).Options);
        }

        /// <summary>
        /// Ein <see cref="ISecurityRepository"/>, das nur ver- und entschluesselt - umkehrbar und am Mandanten erkennbar.
        /// Jeder andere Aufruf ist ein Fehler im Test.
        /// </summary>
        public class CipherOnly : DispatchProxy
        {
            public static ISecurityRepository Create() => Create<ISecurityRepository, CipherOnly>();

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                if (targetMethod?.Name == nameof(ISecurityRepository.Encrypt) && args is [string value, string scope])
                {
                    return $"enc[{scope}]:{value}";
                }

                if (targetMethod?.Name == nameof(ISecurityRepository.Decrypt) && args is [string cipher, string owner])
                {
                    var prefix = $"enc[{owner}]:";
                    return cipher.StartsWith(prefix, StringComparison.Ordinal)
                        ? cipher.Substring(prefix.Length)
                        : throw new InvalidOperationException($"'{cipher}' was not encrypted for '{owner}'.");
                }

                throw new NotSupportedException($"{targetMethod?.Name} is not part of this test.");
            }
        }
    }
}
