using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.DependencyInjection;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices
{
    /// <summary>
    /// Zieht beim Start die Zugangsdaten, die noch an Dienst-Definitionen haengen, in die Zugangsdaten-Zeilen ihrer Besitzer
    /// (Leitfaden §93).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seit §93 liest die Aufloesung Zugangsdaten nur noch aus den Zeilen - ein Geheimnis an der Definition wirkt nicht mehr.
    /// Dieser Dienst sorgt dafuer, dass beim ersten Start mit dieser Fassung nichts verloren geht. Er ist idempotent: was
    /// umgezogen ist, steht nicht mehr an der Definition und wird beim naechsten Start nicht mehr gefunden.
    /// </para>
    /// <para>
    /// Nicht generisch registriert: er holt den Sicherheitskontext ueber die <see cref="IToolkitContextFactory"/>, die jede
    /// Auspraegung (flach, Identity, Baum) ohnehin registriert, und liest die konkreten Typen aus dessen Modell.
    /// </para>
    /// </remarks>
    public sealed class ExternalServiceCredentialMigrationService : BackgroundService
    {
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<ExternalServiceCredentialMigrationService> logger;

        public ExternalServiceCredentialMigrationService(IServiceScopeFactory scopeFactory,
            ILogger<ExternalServiceCredentialMigrationService> logger)
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
                var security = scope.ServiceProvider.GetService<ISecurityRepository>();
                if (factory == null || security == null)
                {
                    logger.LogDebug("No database security model is registered; there are no external-service credentials to move.");
                    return;
                }

                using var lease = factory.Lease<DbContext>();
                ExternalServiceCredentialMigrator.Run(lease.Context, security, logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Moving external-service credentials was cancelled by the shutdown; it is resumed on the next start.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Moving the external-service credentials from the service definitions into the credentials rows failed; it is retried on the next start. Until then, services whose secret still sits on the definition have no credentials.");
            }
        }
    }

    /// <summary>Der eigentliche Umzug - fuer jeden Sicherheitskontext, dessen Modell externe Dienste fuehrt.</summary>
    public static class ExternalServiceCredentialMigrator
    {
        /// <summary>
        /// Zieht um. Die konkreten Typen (Mandant, Dienst, Zustand, Zeile) kommen aus dem Modell des Kontexts.
        /// </summary>
        public static void Run(DbContext db, ISecurityRepository security, ILogger logger, CancellationToken cancellationToken)
        {
            var serviceType = db.Model.GetEntityTypes().Select(e => e.ClrType).FirstOrDefault(t => GenericArgumentsOf(t) != null);
            if (serviceType == null)
            {
                logger.LogDebug("The security context '{Context}' has no external services.", db.GetType().Name);
                return;
            }

            var arguments = GenericArgumentsOf(serviceType)!;
            typeof(ExternalServiceCredentialMigrator).GetMethod(nameof(RunTyped), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(arguments)
                .Invoke(null, new object[] { db, security, logger, cancellationToken });
        }

        /// <summary>Die Typargumente von <c>ExternalOAuthService&lt;TTenant, TService, TState, TLogin&gt;</c>, oder null.</summary>
        private static Type[] GenericArgumentsOf(Type type)
        {
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ExternalOAuthService<,,,>))
                {
                    return t.GetGenericArguments();
                }
            }

            return null;
        }

        private static void RunTyped<TTenant, TService, TState, TLogin>(DbContext db, ISecurityRepository security,
            ILogger logger, CancellationToken cancellationToken)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
        {
            // Was noch an der Definition haengt: ausser bei Authorization-Code (dort ist ClientId/Secret die App) das
            // Geheimnis, und bei Typen mit Benutzer auch ClientId. ApiKey behaelt ClientId - das ist der Header-Name.
            var pending = db.Set<TService>().IgnoreQueryFilters()
                .Where(s => s.AuthenticationType != ExternalServiceAuthenticationType.OAuthAuthorizationFlow
                            && ((s.ClientSecret != null && s.ClientSecret != "")
                                || (s.AuthenticationType != ExternalServiceAuthenticationType.ApiKey
                                    && s.AuthenticationType != ExternalServiceAuthenticationType.BearerToken
                                    && s.ClientId != null && s.ClientId != "")))
                .ToList();
            if (pending.Count == 0)
            {
                logger.LogDebug("No external-service credentials are left on service definitions.");
            }
            else
            {
                MoveAll<TTenant, TService, TState, TLogin>(db, security, logger, pending, cancellationToken);
            }

            // Auch ohne Umzug in diesem Lauf: eine Datenbank, die schon mit einer frueheren 5.x-Fassung umgezogen ist, traegt
            // die verwaisten Token-Zeilen noch (Issue External Services 01).
            RemoveStaleTokenRows<TTenant, TService, TState, TLogin>(db, logger, cancellationToken);
        }

        private static void MoveAll<TTenant, TService, TState, TLogin>(DbContext db, ISecurityRepository security,
            ILogger logger, System.Collections.Generic.List<TService> pending, CancellationToken cancellationToken)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
        {
            int moved = 0, alreadySet = 0, failed = 0;
            foreach (var service in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (MoveOne<TTenant, TService, TState, TLogin>(db, security, service))
                    {
                        moved++;
                    }
                    else
                    {
                        alreadySet++;
                    }
                }
                catch (DbUpdateException ex)
                {
                    // Typisch: eine zweite Instanz hat dieselbe Zeile im selben Augenblick angelegt. Beim naechsten Start
                    // findet sich der Dienst wieder - oder nicht mehr, wenn die andere ihn erledigt hat.
                    failed++;
                    logger.LogWarning(ex, "The credentials of external service {ServiceId} ('{Name}') were not moved in this run; another instance may have moved them. Retried on the next start.",
                        service.OAuthServiceId, service.UniqueConnectionName);
                    db.ChangeTracker.Clear();
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "The credentials of external service {ServiceId} ('{Name}', tenant {TenantId}) could not be moved; they stay on the definition, where they no longer take effect. Set them under 'Credentials'.",
                        service.OAuthServiceId, service.UniqueConnectionName, service.TenantId);
                    db.ChangeTracker.Clear();
                }
            }

            logger.LogInformation("External-service credentials moved from service definitions into credentials rows: {Moved} moved, {AlreadySet} already had credentials, {Failed} failed.",
                moved, alreadySet, failed);
        }

        /// <summary>
        /// Entfernt die Token-Zeilen aus 4.x, die nach dem Umzug die umgezogenen Zugangsdaten verdecken (Issue External
        /// Services 01).
        /// </summary>
        /// <remarks>
        /// <para>
        /// In 4.x waren die Zeilen der Mandanten reine Token-Puffer; die Zugangsdaten standen an der Definition. Nach dem
        /// Umzug stehen sie in der Plattform-Zeile (globaler Dienst) bzw. in der des Besitzers - die alte Zeile des Mandanten
        /// hat keine, wird aber zuerst gefunden.
        /// </para>
        /// <para>
        /// Entfernt wird eine Mandanten-Zeile nur, wenn sie keine Zugangsdaten hat, der Dienst feste Zugangsdaten kennt (nicht
        /// Authorization-Code - dort IST die Zeile das Login) und es eine Zeile gibt, die sie abdeckt: die Plattform-Zeile mit
        /// Zugangsdaten, wenn die Definition sie zulaesst, oder die vererbbare des Besitzers mit Zugangsdaten. Dorthin schreibt
        /// die Aufloesung danach auch das Token - es entstehen keine neuen leeren Zeilen, ein zweiter Start findet nichts mehr.
        /// </para>
        /// <para>
        /// Verloren geht nur das gepufferte Token; es wird beim naechsten Abruf neu geholt.
        /// </para>
        /// </remarks>
        private static void RemoveStaleTokenRows<TTenant, TService, TState, TLogin>(DbContext db, ILogger logger,
            CancellationToken cancellationToken)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
        {
            var services = db.Set<TService>().IgnoreQueryFilters()
                .Where(s => s.AuthenticationType != ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
                .Select(s => new { s.OAuthServiceId, s.TenantId, s.AllowPlatformCredentials, s.UniqueConnectionName })
                .ToList();
            var removed = 0;
            foreach (var service in services)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var rows = db.Set<TLogin>().IgnoreQueryFilters()
                        .Where(l => l.OAuthServiceId == service.OAuthServiceId)
                        .ToList();
                    var covered = rows.Any(l => l.TenantId == null && service.AllowPlatformCredentials && ExternalServiceCredentials.HasCredentials(l.CredentialJson))
                                  || (service.TenantId != null && rows.Any(l => l.TenantId == service.TenantId && l.Inheritable && ExternalServiceCredentials.HasCredentials(l.CredentialJson)));
                    if (!covered)
                    {
                        continue;
                    }

                    var stale = rows.Where(l => l.TenantId != null && !ExternalServiceCredentials.HasCredentials(l.CredentialJson)).ToList();
                    if (stale.Count == 0)
                    {
                        continue;
                    }

                    db.Set<TLogin>().RemoveRange(stale);
                    db.SaveChanges();
                    removed += stale.Count;
                    logger.LogInformation("Removed {Count} token row(s) without credentials of external service {ServiceId} ('{Name}') for tenant(s) {Tenants}: they hid the credentials of the platform or owner row. The token is fetched again on the next call.",
                        stale.Count, service.OAuthServiceId, service.UniqueConnectionName, string.Join(", ", stale.Select(l => l.TenantId)));
                }
                catch (DbUpdateException ex)
                {
                    // Typisch: eine zweite Instanz hat dieselben Zeilen im selben Augenblick entfernt oder neu beschrieben.
                    logger.LogWarning(ex, "The stale token rows of external service {ServiceId} ('{Name}') were not removed in this run; another instance may have changed them. Retried on the next start.",
                        service.OAuthServiceId, service.UniqueConnectionName);
                    db.ChangeTracker.Clear();
                }
            }

            if (removed > 0)
            {
                logger.LogInformation("Removed {Removed} stale external-service token row(s) in total.", removed);
            }
            else
            {
                logger.LogDebug("No stale external-service token rows found.");
            }
        }

        /// <returns>true, wenn umgezogen; false, wenn die Zeile schon eigene Zugangsdaten hatte (dann nur aufgeraeumt)</returns>
        private static bool MoveOne<TTenant, TService, TState, TLogin>(DbContext db, ISecurityRepository security, TService service)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
        {
            var ownerId = service.TenantId;
            var ownerName = ownerId == null
                ? null
                : db.Set<TTenant>().IgnoreQueryFilters().Where(t => t.TenantId == ownerId).Select(t => t.TenantName).First();
            var hasUser = service.AuthenticationType is ExternalServiceAuthenticationType.Basic
                or ExternalServiceAuthenticationType.OAuthPasswordFlow
                or ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
                or ExternalServiceAuthenticationType.OAuthBasicClientCredentialsFlow;

            var row = db.Set<TLogin>().IgnoreQueryFilters()
                .FirstOrDefault(l => l.OAuthServiceId == service.OAuthServiceId && l.TenantId == ownerId);
            var movedNow = false;
            if (string.IsNullOrEmpty(row?.CredentialJson))
            {
                // Entschluesselt wird, wie gespeichert wurde: mit dem Systemschluessel bei Global (oder ohne Mandant), sonst
                // mit dem des Mandanten. Verschluesselt wird mit dem des Besitzers der Zeile.
                var secret = string.IsNullOrEmpty(service.ClientSecret)
                    ? null
                    : service.Global || ownerName == null
                        ? service.ClientSecret.Decrypt()
                        : security.Decrypt(service.ClientSecret, ownerName);
                if (row == null)
                {
                    row = Activator.CreateInstance<TLogin>();
                    row.OAuthServiceId = service.OAuthServiceId;
                    row.TenantId = ownerId;
                    row.Token = "{}";
                    db.Set<TLogin>().Add(row);
                }

                row.CredentialJson = ExternalServiceCredentials.Protect(new ExternalServiceCredential
                {
                    User = hasUser && !string.IsNullOrEmpty(service.ClientId) ? service.ClientId : null,
                    Secret = secret
                }, v => ownerName == null ? v.Encrypt() : security.Encrypt(v, ownerName));

                // Bisheriges Verhalten bewahren: ein vererbbarer Dienst (Baum) gab sein Geheimnis an die Kinder weiter, ein
                // globaler an alle Mandanten.
                var inheritable = db.Entry(service).Metadata.FindProperty("Inheritable");
                if (ownerId != null && inheritable != null && db.Entry(service).Property(inheritable.Name).CurrentValue is true)
                {
                    row.Inheritable = true;
                }

                if (ownerId == null)
                {
                    service.AllowPlatformCredentials = true;
                }

                movedNow = true;
            }

            service.ClientSecret = string.Empty;
            if (hasUser)
            {
                service.ClientId = string.Empty;
            }

            db.SaveChanges();
            return movedNow;
        }
    }

    /// <summary>Registriert den Umzug der Zugangsdaten beim Start - einmal, gleich wie oft eine Auspraegung ihn verlangt.</summary>
    public static class ExternalServiceCredentialMigrationExtensions
    {
        public static IServiceCollection AddExternalServiceCredentialMigration(this IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ExternalServiceCredentialMigrationService>());
            return services;
        }
    }
}
