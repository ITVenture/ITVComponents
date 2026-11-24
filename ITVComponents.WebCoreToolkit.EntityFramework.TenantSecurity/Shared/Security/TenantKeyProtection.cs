using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security
{
    /// <summary>
    /// Protects the tenant keys that are still stored in plain text, on start (issue Security 01 part C, guide §127).
    /// Idempotent: a protected key is not touched again.
    /// </summary>
    /// <remarks>
    /// Needs the wider column (§127, 512 characters). As long as the column still has 125 characters, saving fails; that
    /// is logged as error, nothing is changed, and it is tried again on the next start.
    /// </remarks>
    public sealed class TenantKeyProtectionService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<TenantKeyProtectionService> logger;

        public TenantKeyProtectionService(IServiceScopeFactory scopeFactory, ILogger<TenantKeyProtectionService> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        /// <inheritdoc />
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
            => Task.Run(() => Run(stoppingToken), stoppingToken);

        private void Run(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var factory = scope.ServiceProvider.GetService<IToolkitContextFactory>();
                if (factory == null)
                {
                    logger.LogDebug("No database security model is registered; there are no tenant keys to protect.");
                    return;
                }

                using var lease = factory.Lease<DbContext>();
                TenantKeyProtection.Run(lease.Context, logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Protecting the tenant keys was cancelled by the shutdown; it is resumed on the next start.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Protecting the tenant keys with the application key failed; they stay in plain text and it is retried on the next start. Check that the column Tenants.TenantPassword has 512 characters (guide §127) and that PasswordSecurity is configured.");
            }
        }
    }

    /// <summary>The protection itself - for every security context whose model has tenants.</summary>
    public static class TenantKeyProtection
    {
        /// <summary>Protects all plain tenant keys of the context. Returns how many were protected.</summary>
        public static int Run(DbContext db, ILogger logger, CancellationToken cancellationToken)
        {
            var tenantType = db.Model.GetEntityTypes().Select(e => e.ClrType).FirstOrDefault(t => typeof(Tenant).IsAssignableFrom(t) && !t.IsAbstract);
            if (tenantType == null)
            {
                logger.LogDebug("The security context '{Context}' has no tenants.", db.GetType().Name);
                return 0;
            }

            return (int)typeof(TenantKeyProtection).GetMethod(nameof(RunTyped), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(tenantType)
                .Invoke(null, new object[] { db, logger, cancellationToken })!;
        }

        private static int RunTyped<TTenant>(DbContext db, ILogger logger, CancellationToken cancellationToken)
            where TTenant : Tenant
        {
            var plain = db.Set<TTenant>().IgnoreQueryFilters()
                .Where(t => t.TenantPassword != null && t.TenantPassword != "" && !t.TenantPassword.StartsWith(TenantKeys.ProtectedPrefix))
                .ToList();
            if (plain.Count == 0)
            {
                logger.LogDebug("All tenant keys are protected.");
                return 0;
            }

            foreach (var tenant in plain)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tenant.TenantPassword = TenantKeys.Protect(tenant.TenantPassword);
            }

            db.SaveChanges();
            logger.LogInformation("Protected {Count} tenant key(s) with the application key.", plain.Count);
            return plain.Count;
        }
    }

    /// <summary>Registers the protection of the tenant keys on start - once, however often a variant asks for it.</summary>
    public static class TenantKeyProtectionExtensions
    {
        public static IServiceCollection AddTenantKeyProtection(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TenantKeyProtectionService>());
            return services;
        }
    }
}
