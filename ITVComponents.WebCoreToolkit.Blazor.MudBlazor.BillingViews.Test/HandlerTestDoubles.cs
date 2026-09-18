using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Options;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.Helpers;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.BillingViews.Blazor.Test
{
    /// <summary>
    /// Baut die Umgebung, in der ein Handler laeuft: einen Dienstanbieter mit genau den Rechten, die der Test
    /// vergibt, dazu Attrappen fuer alles, was der Handler unterhalb von sich erwartet.
    /// </summary>
    /// <remarks>
    /// <b>Die Attrappen zaehlen ihre Aufrufe.</b> Das ist der eigentliche Nachweis: eine Rechtepruefung, die
    /// erst hinter dem Datenzugriff greift, waere nutzlos - sie haette dann schon gelesen. Ein Test, der nur
    /// „es wirft" prueft, wuerde das nicht bemerken.
    /// </remarks>
    internal sealed class HandlerTestEnvironment
    {
        public const string AuthType = "TestAuth";

        private HandlerTestEnvironment(IServiceProvider services)
        {
            Services = services;
        }

        public IServiceProvider Services { get; }

        public SpyAccountService AccountService { get; } = new();

        public SpySaleService SaleService { get; } = new();

        public SpyCheckoutFactory Checkout { get; } = new();

        public SpyPortalFactory Portal { get; } = new();

        public SpyPlanSynchronizer PlanSynchronizer { get; } = new();

        public SpyFeatureGate FeatureGate { get; } = new();

        public TestSettings Settings { get; } = new();

        /// <summary>Der Mandant im Bereich. Null bildet den Fall „kein Mandant" ab.</summary>
        public int? CurrentTenantId { get; set; } = 1;

        /// <summary>
        /// Eine Kontext-Fabrik, die beim ERZEUGEN wirft. Damit laesst sich beweisen, dass die Rechtepruefung
        /// vor dem ersten Datenzugriff steht: kommt die erwartete Rechte-Absage zurueck und nicht diese
        /// Ausnahme, wurde die Datenbank nie angefasst.
        /// </summary>
        public bool DatabaseIsOffLimits { get; set; }

        /// <summary>Wie oft eine Kontext-Fabrik doch einen Kontext geliefert hat.</summary>
        public int ContextsCreated { get; private set; }

        /// <summary>
        /// Baut die Umgebung mit den angegebenen Rechten. Keine Rechte = ein angemeldeter Benutzer, der
        /// nichts darf - nicht etwa ein anonymer: der wuerde schon eine Stufe frueher abgewiesen und liesse
        /// offen, ob die Methode selbst prueft.
        /// </summary>
        public static HandlerTestEnvironment WithPermissions(params string[] permissions)
        {
            var repository = new FakeSecurityRepository
            {
                Authenticated = true,
                PermissionsForLabels = permissions.Select(p => new Permission { PermissionName = p }).ToArray()
            };

            var identity = new ClaimsIdentity(new[] { new Claim(System.Security.Claims.ClaimTypes.Name, "tester") }, AuthType);
            var userProvider = new FakeContextUserProvider { User = new ClaimsPrincipal(identity) };

            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<ISecurityRepository>(repository);
            collection.AddSingleton<IUserNameMapper>(new FakeUserNameMapper());
            collection.AddSingleton<IContextUserProvider>(userProvider);
            return new HandlerTestEnvironment(collection.BuildServiceProvider());
        }

        public IDbContextFactory<BillingTestContext> DbFactory()
            => new TestContextFactory(this, Guid.NewGuid().ToString("N"));

        public IGlobalSettings<TenantPaymentsOptions> PaymentSettings() => Settings;

        public IEnumerable<IPaymentFeatureGate> FeatureGates() => new IPaymentFeatureGate[] { FeatureGate };

        private sealed class TestContextFactory : IDbContextFactory<BillingTestContext>
        {
            private readonly HandlerTestEnvironment owner;
            private readonly string databaseName;

            public TestContextFactory(HandlerTestEnvironment owner, string databaseName)
            {
                this.owner = owner;
                this.databaseName = databaseName;
            }

            public BillingTestContext CreateDbContext()
            {
                if (owner.DatabaseIsOffLimits)
                {
                    throw new InvalidOperationException(
                        "The handler reached the database although the permission check should have refused first.");
                }

                owner.ContextsCreated++;
                return new BillingTestContext(
                    new DbContextOptionsBuilder<BillingTestContext>().UseInMemoryDatabase(databaseName).Options,
                    owner.CurrentTenantId);
            }
        }
    }

    /// <summary>Der kleinste Kontext, der beide Handler bedient.</summary>
    public class BillingTestContext : DbContext, IPaymentsContext, IBillingContext, ITenantScopeContext
    {
        public BillingTestContext(DbContextOptions<BillingTestContext> options, int? currentTenantId)
            : base(options)
        {
            CurrentTenantId = currentTenantId;
        }

        public int? CurrentTenantId { get; }

        public DbSet<TenantPaymentAccount> TenantPaymentAccounts { get; set; } = null!;
        public DbSet<TenantPaymentProfile> TenantPaymentProfiles { get; set; } = null!;
        public DbSet<TenantPaymentTerminal> TenantPaymentTerminals { get; set; } = null!;
        public DbSet<TenantSale> TenantSales { get; set; } = null!;
        public DbSet<TenantSaleRefund> TenantSaleRefunds { get; set; } = null!;
        public DbSet<TenantFeeWaiver> TenantFeeWaivers { get; set; } = null!;

        public DbSet<Plan> Plans { get; set; } = null!;
        public DbSet<PlanPrice> PlanPrices { get; set; } = null!;
        public DbSet<PlanFeature> PlanFeatures { get; set; } = null!;
        public DbSet<AddOn> AddOns { get; set; } = null!;
        public DbSet<PlanAddOn> PlanAddOns { get; set; } = null!;
        public DbSet<PlanAddOnPrice> PlanAddOnPrices { get; set; } = null!;
        public DbSet<AddOnFeature> AddOnFeatures { get; set; } = null!;
        public DbSet<TenantSubscription> TenantSubscriptions { get; set; } = null!;
        public DbSet<TenantSubscriptionItem> TenantSubscriptionItems { get; set; } = null!;
    }

    // ------------------------------------------------------------------ Attrappen der Dienstschicht

    internal sealed class SpyAccountService : ITenantPaymentAccountService
    {
        public List<string> Calls { get; } = new();

        public Task<TenantPaymentAccountStatus?> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(GetStatusAsync)}({tenantId})");
            return Task.FromResult<TenantPaymentAccountStatus?>(null);
        }

        public Task<string> StartOnboardingAsync(int tenantId, string returnUrl, string refreshUrl, string? email = null,
            string? country = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(StartOnboardingAsync)}({tenantId})");
            return Task.FromResult("https://example.invalid/onboarding");
        }

        public Task<TenantPaymentAccountStatus> RefreshAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(RefreshAsync)}({tenantId})");
            return Task.FromResult(new TenantPaymentAccountStatus());
        }

        public Task<string?> CreateDashboardLinkAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(CreateDashboardLinkAsync)}({tenantId})");
            return Task.FromResult<string?>("https://example.invalid/dashboard");
        }
    }

    internal sealed class SpySaleService : ITenantSaleService
    {
        public List<string> Calls { get; } = new();

        public Task<SaleResult> CreateSaleAsync(SaleRequest request, CancellationToken cancellationToken = default)
        {
            Calls.Add(nameof(CreateSaleAsync));
            return Task.FromResult(new SaleResult());
        }

        public Task<SaleResult> GetSaleAsync(int tenantSaleId, CancellationToken cancellationToken = default)
        {
            Calls.Add(nameof(GetSaleAsync));
            return Task.FromResult(new SaleResult());
        }

        public Task<SaleResult?> FindByReferenceAsync(int tenantId, string externalReference, CancellationToken cancellationToken = default)
        {
            Calls.Add(nameof(FindByReferenceAsync));
            return Task.FromResult<SaleResult?>(null);
        }

        public Task<RefundResult> RefundSaleAsync(int tenantSaleId, long? amountMinor, string? reason,
            bool? refundApplicationFee = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(RefundSaleAsync)}({tenantSaleId})");
            return Task.FromResult(new RefundResult());
        }
    }

    internal sealed class SpyCheckoutFactory : ISubscriptionCheckoutFactory
    {
        public List<string> Calls { get; } = new();

        public Task<string> CreateCheckoutSessionAsync(int tenantId, int planId, IReadOnlyCollection<int> addOnIds,
            string successUrl, string cancelUrl, string? currency = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(CreateCheckoutSessionAsync)}({tenantId},{planId})");
            return Task.FromResult("https://example.invalid/checkout");
        }
    }

    internal sealed class SpyPortalFactory : IBillingPortalFactory
    {
        public List<string> Calls { get; } = new();

        public Task<string?> CreatePortalSessionAsync(int tenantId, string returnUrl, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(CreatePortalSessionAsync)}({tenantId})");
            return Task.FromResult<string?>("https://example.invalid/portal");
        }
    }

    internal sealed class SpyPlanSynchronizer : IPlanSynchronizer
    {
        public List<string> Calls { get; } = new();

        public Task SyncPlanAsync(int planId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(SyncPlanAsync)}({planId})");
            return Task.CompletedTask;
        }

        public Task SyncAddOnAsync(int addOnId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"{nameof(SyncAddOnAsync)}({addOnId})");
            return Task.CompletedTask;
        }
    }

    internal sealed class SpyFeatureGate : IPaymentFeatureGate
    {
        /// <summary>Vorbelegt auf JA, damit ein Test, der ueber die Rechte spricht, nicht am Feature scheitert.</summary>
        public bool Enabled { get; set; } = true;

        public int Queries { get; private set; }

        public Task<bool> IsEnabledForTenantAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            Queries++;
            return Task.FromResult(Enabled);
        }
    }

    internal sealed class TestSettings : IGlobalSettings<TenantPaymentsOptions>
    {
        public TenantPaymentsOptions Current { get; } = new() { Enabled = true };

        public TenantPaymentsOptions Value => Current;

        public TenantPaymentsOptions ValueOrDefault => Current;

        public TenantPaymentsOptions GetValue(string explicitSettingName) => Current;

        public TenantPaymentsOptions GetValueOrDefault(string explicitSettingName) => Current;
    }

    // ------------------------------------------------------------------ Sicherheits-Attrappen
    // Bewusst eigene Kopien: die gleichnamigen Doubles in ITVComponents.WebCoreToolkit.Tests sind internal
    // und gehoeren einem anderen Testprojekt. Hier steht nur, was die Rechteaufloesung wirklich anfasst.

    internal sealed class FakeContextUserProvider : IContextUserProvider
    {
        public ClaimsPrincipal User { get; set; } = new();
        public IServiceProvider? Services { get; set; }
        public IDictionary<string, object> RouteData { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public string? RequestPath { get; set; }
    }

    internal sealed class FakeUserNameMapper : IUserNameMapper
    {
        public string[] GetUserLabels(IIdentity user) => new[] { "tester" };
        public string? UniqueName { get; set; }
        public void Dispose() { }

        /// <summary>Wird hier nie ausgeloest - leere Zugriffsmethoden, damit kein ungenutztes Feld entsteht.</summary>
        public event EventHandler? Disposed { add { } remove { } }
    }

    internal sealed class FakeSecurityRepository : ISecurityRepository
    {
        /// <summary>Die Antwort auf die Anmeldefrage.</summary>
        public bool? Authenticated { get; set; }

        /// <summary>Die Rechte, die zu den Bezeichnern gehoeren.</summary>
        public Permission[]? PermissionsForLabels { get; set; }

        public bool IsAuthenticated(string[] userLabels, string userAuthenticationType)
            => Authenticated ?? throw new NotImplementedException();

        public bool IsAuthenticated(string[] userLabels, string forScope, string userAuthenticationType)
            => Authenticated ?? throw new NotImplementedException();

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string userAuthenticationType)
            => PermissionsForLabels ?? throw new NotImplementedException();

        public IEnumerable<Permission> GetPermissions(string[] userLabels, string forScope, string userAuthenticationType)
            => PermissionsForLabels ?? throw new NotImplementedException();

        // ----- unbenutzt ------------------------------------------------------
        public string? UniqueName { get; set; }
        public void Dispose() { }

        /// <summary>Wird hier nie ausgeloest - leere Zugriffsmethoden, damit kein ungenutztes Feld entsteht.</summary>
        public event EventHandler? Disposed { add { } remove { } }
        public ICollection<User> Users => throw new NotImplementedException();
        public ICollection<Role> Roles => throw new NotImplementedException();
        public ICollection<Permission> Permissions => throw new NotImplementedException();
        public IEnumerable<ScopeInfo> GetEligibleScopes(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public Permission[] GetKnownPermissions(string permissionScope) => throw new NotImplementedException();
        public IEnumerable<Feature> GetFeatures(string permissionScopeName) => throw new NotImplementedException();
        public IEnumerable<Role> GetRoles(User user) => throw new NotImplementedException();
        public IEnumerable<Role> GetRolesWithPermissions(IEnumerable<string> requiredPermissions, string permissionScope) => throw new NotImplementedException();
        public IEnumerable<CustomUserProperty> GetCustomProperties(User user, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public string GetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public T GetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public bool SetCustomProperty(User user, string propertyName, CustomUserPropertyType propertyType, string value) => throw new NotImplementedException();
        public bool SetCustomProperty<T>(User user, string propertyName, CustomUserPropertyType propertyType, T value) => throw new NotImplementedException();
        public IEnumerable<CustomUserProperty> GetCustomProperties(string[] userLabels, string userAuthenticationType, CustomUserPropertyType propertyType) => throw new NotImplementedException();
        public IEnumerable<T> GetUserIds<T>(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public T GetUserId<T>(string[] userLabels, string userAuthenticationType) => throw new NotImplementedException();
        public IEnumerable<ClaimData> GetCustomProperties(ClaimData[] originalClaims, string userAuthenticationType) => throw new NotImplementedException();
        public IEnumerable<Permission> GetPermissions(User user) => throw new NotImplementedException();
        public IEnumerable<Permission> GetPermissions(Role role) => throw new NotImplementedException();
        public bool PermissionScopeExists(string permissionScopeName) => throw new NotImplementedException();
        public TimeZoneHelper GetTimeZoneHelper(string permissionScopeName) => throw new NotImplementedException();
        public string Decrypt(string encryptedValue, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Decrypt(byte[] encryptedValue, string permissionScopeName, byte[] initializationVector, byte[] salt) => throw new NotImplementedException();
        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName, byte[] initializationVector, byte[] salt) => throw new NotImplementedException();
        public Stream GetDecryptStream(Stream baseStream, string permissionScopeName) => throw new NotImplementedException();
        public string Encrypt(string value, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Encrypt(byte[] value, string permissionScopeName) => throw new NotImplementedException();
        public byte[] Encrypt(byte[] value, string permissionScopeName, out byte[] initializationVector, out byte[] salt) => throw new NotImplementedException();
        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName, out byte[] initializationVector, out byte[] salt) => throw new NotImplementedException();
        public Stream GetEncryptStream(Stream baseStream, string permissionScopeName) => throw new NotImplementedException();
        public string EncryptJsonObject(object value, string permissionScopeName) => throw new NotImplementedException();
        public ExternalServiceConnection GetExternalService(string name, bool decryptSecret = false) => throw new NotImplementedException();
        public void PrepareExternalServiceConnect(OAuthState oAuthState) => throw new NotImplementedException();
        public OAuthState GetOAuthRequest(string connectionName, string state) => throw new NotImplementedException();
        public void StoreExternalServiceToken(string connectionName, TranslatedTokenResponse token) => throw new NotImplementedException();
        public TranslatedTokenResponse GetBufferedToken(string connectionName, bool forRevoke, bool throwIfNull, out ExternalServiceConnection connectionInfo, out Action<TranslatedTokenResponse> updateToken) => throw new NotImplementedException();
    }
}
