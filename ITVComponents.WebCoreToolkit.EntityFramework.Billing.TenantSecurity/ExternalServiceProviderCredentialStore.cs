using System.Collections.Concurrent;
using System.Net.Http.Json;
using ITVComponents.Json;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.TenantSecurity
{
    /// <summary>
    /// <see cref="IProviderCredentialStore"/> ueber die ExternalServices der Sicherheits-Datenbank - fuer einen
    /// ausdruecklich genannten Mandanten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nach Mandanten-Id, nicht ueber den Bereich</b> - darum liest diese Klasse die Tabellen selbst
    /// (ohne Abfragefilter) statt ueber <see cref="ISecurityRepository.GetExternalService"/> und
    /// <c>IOAuthTokenService</c>, die beide den Mandanten aus dem aktuellen Bereich nehmen. Ver- und
    /// entschluesselt wird trotzdem ueber das Repository: dessen Methoden nehmen den Mandanten als Parameter,
    /// und so bleibt es bei EINER Stelle, die den Mandantenschluessel kennt.
    /// </para>
    /// <para>
    /// <b>Nur der Dienst des Mandanten selbst, nichts Geerbtes.</b> Ein fester Schluessel (ApiKey, Bearer,
    /// Basic) muss ein Dienst dieses Mandanten sein. Eine OAuth-Anmeldung darf an einem GLOBALEN Dienst haengen
    /// (die App der Plattform), die Anmeldung selbst aber muss die dieses Mandanten sein. Im Baum-Modell erbt
    /// ein Kind-Mandant also keinen Zugang seines Elternteils - wessen Konto Geld empfaengt, soll an der Zeile
    /// stehen und nicht aus der Hierarchie folgen.
    /// </para>
    /// <para>
    /// <b>Erneuern:</b> ein abgelaufenes Zugriffstoken wird hier erneuert und gespeichert. Gesperrt wird je
    /// Mandant und Dienst im Prozess; nach dem Warten wird neu gelesen, weil eine andere Instanz inzwischen
    /// erneuert haben kann. Zwischen zwei Instanzen bleibt ein schmales Fenster: tauscht der Anbieter den
    /// Refresh-Token bei jeder Erneuerung aus und erneuern beide gleichzeitig, verliert eine. Sie meldet dann
    /// <c>invalid_grant</c>; vorher wird noch einmal nachgesehen, ob die andere das neue Token schon abgelegt
    /// hat.
    /// </para>
    /// </remarks>
    public class ExternalServiceProviderCredentialStore<TContext, TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin> : IProviderCredentialStore
        where TContext : DbContext
        where TTenant : Tenant
        where TExternalOAuthService : ExternalOAuthService<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>, new()
        where TExternalOAuthServiceState : ExternalOAuthServiceState<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
        where TExternalOAuthServiceTenantLogin : ExternalOAuthServiceTenantLogin<TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshLocks = new();

        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly ISecurityRepository securityRepository;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger logger;

        /// <summary>Initializes a new instance of the <see cref="ExternalServiceProviderCredentialStore{TContext,TTenant,TExternalOAuthService,TExternalOAuthServiceState,TExternalOAuthServiceTenantLogin}"/> class.</summary>
        public ExternalServiceProviderCredentialStore(IDbContextFactory<TContext> dbFactory,
            ISecurityRepository securityRepository, IHttpClientFactory httpClientFactory,
            ILogger<ExternalServiceProviderCredentialStore<TContext, TTenant, TExternalOAuthService, TExternalOAuthServiceState, TExternalOAuthServiceTenantLogin>> logger)
        {
            this.dbFactory = dbFactory;
            this.securityRepository = securityRepository;
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task<ProviderCredential> GetCredentialAsync(int tenantId, string connectionName,
            CancellationToken cancellationToken = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var tenantName = await TenantNameAsync(db, tenantId, cancellationToken);

            // Zuerst der Dienst des Mandanten, dann - nur fuer eine OAuth-Anmeldung - die App der Plattform.
            var service = await db.Set<TExternalOAuthService>().AsNoTracking().IgnoreQueryFilters()
                              .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.UniqueConnectionName == connectionName, cancellationToken)
                          ?? await db.Set<TExternalOAuthService>().AsNoTracking().IgnoreQueryFilters()
                              .FirstOrDefaultAsync(s => s.TenantId == null && s.UniqueConnectionName == connectionName
                                                        && s.AuthenticationType == ExternalServiceAuthenticationType.OAuthAuthorizationFlow, cancellationToken);
            if (service == null)
            {
                logger.LogError("Tenant {TenantId} has no external service '{ConnectionName}'; its own provider access cannot be used.",
                    tenantId, connectionName);
                throw Unavailable($"No credentials are stored for '{connectionName}'.");
            }

            switch (service.AuthenticationType)
            {
                case ExternalServiceAuthenticationType.ApiKey:
                case ExternalServiceAuthenticationType.BearerToken:
                case ExternalServiceAuthenticationType.Basic:
                    if (string.IsNullOrEmpty(service.ClientSecret))
                    {
                        logger.LogError("External service '{ConnectionName}' of tenant {TenantId} has no secret stored.", connectionName, tenantId);
                        throw Unavailable($"'{connectionName}' has no secret stored.");
                    }

                    return new ProviderCredential(service.AuthenticationType, service.ClientId,
                        securityRepository.Decrypt(service.ClientSecret, tenantName));

                case ExternalServiceAuthenticationType.OAuthAuthorizationFlow:
                    return new ProviderCredential(service.AuthenticationType, service.ClientId,
                        await AccessTokenAsync(service, tenantId, tenantName, cancellationToken));

                default:
                    // Client-Credentials gehoeren der Plattform, nicht einem Mandanten - als eigener Zugang eines
                    // Mandanten ergeben sie keinen Sinn.
                    logger.LogError("External service '{ConnectionName}' of tenant {TenantId} uses {AuthenticationType}, which cannot serve as a tenant's own provider access.",
                        connectionName, tenantId, service.AuthenticationType);
                    throw Unavailable($"'{connectionName}' uses {service.AuthenticationType}, which is not supported for an own provider access.");
            }
        }

        /// <inheritdoc />
        public async Task SaveSecretAsync(int tenantId, string connectionName,
            ExternalServiceAuthenticationType authenticationType, string? clientId, string secret,
            CancellationToken cancellationToken = default)
        {
            if (authenticationType is not (ExternalServiceAuthenticationType.ApiKey or ExternalServiceAuthenticationType.BearerToken or ExternalServiceAuthenticationType.Basic))
            {
                throw new ArgumentException($"{authenticationType} is not a fixed secret; an OAuth login is created through the provider's sign-in.", nameof(authenticationType));
            }

            if (string.IsNullOrWhiteSpace(connectionName) || string.IsNullOrEmpty(secret))
            {
                throw new ArgumentException("A connection name and a secret are required.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var tenantName = await TenantNameAsync(db, tenantId, cancellationToken);
            var service = await db.Set<TExternalOAuthService>().IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.UniqueConnectionName == connectionName, cancellationToken);
            if (service == null)
            {
                service = new TExternalOAuthService
                {
                    TenantId = tenantId,
                    UniqueConnectionName = connectionName,
                    Global = false,
                    // Die Spalten sind Pflicht; ein fester Schluessel hat keine Endpunkte.
                    AuthorizationEndpoint = string.Empty,
                    TokenEndpoint = string.Empty,
                    RevocationEndpoint = string.Empty,
                    Scope = string.Empty
                };
                db.Set<TExternalOAuthService>().Add(service);
            }

            service.AuthenticationType = authenticationType;
            service.ClientId = clientId ?? string.Empty;
            service.ClientSecret = securityRepository.Encrypt(secret, tenantName);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Stored the {AuthenticationType} secret of external service '{ConnectionName}' for tenant {TenantId}.",
                authenticationType, connectionName, tenantId);
        }

        private async Task<string> AccessTokenAsync(TExternalOAuthService service, int tenantId, string tenantName,
            CancellationToken cancellationToken)
        {
            var token = await ReadTokenAsync(service.OAuthServiceId, tenantId, tenantName, cancellationToken);
            if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return token.AccessToken;
            }

            var gate = RefreshLocks.GetOrAdd($"{tenantId}/{service.OAuthServiceId}", _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Waehrend des Wartens kann ein anderer Aufruf erneuert haben.
                token = await ReadTokenAsync(service.OAuthServiceId, tenantId, tenantName, cancellationToken);
                if (token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                {
                    return token.AccessToken;
                }

                return await RefreshAsync(service, tenantId, tenantName, token, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<string> RefreshAsync(TExternalOAuthService service, int tenantId, string tenantName,
            TranslatedTokenResponse token, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(token.RefreshToken))
            {
                logger.LogError("The OAuth login of tenant {TenantId} at '{ConnectionName}' has expired and carries no refresh token; the tenant has to sign in again.",
                    tenantId, service.UniqueConnectionName);
                throw Unavailable($"The login at '{service.UniqueConnectionName}' has expired and has to be renewed.");
            }

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = token.RefreshToken,
                ["client_id"] = service.ClientId
            };
            if (!string.IsNullOrEmpty(service.ClientSecret))
            {
                // Die App der Plattform ist global verschluesselt, ein Dienst des Mandanten mit seinem Schluessel.
                form["client_secret"] = service.TenantId == null
                    ? service.ClientSecret.Decrypt()
                    : securityRepository.Decrypt(service.ClientSecret, tenantName);
            }

            using var client = httpClientFactory.CreateClient();
            using var response = await client.PostAsync(service.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                // Eine andere Instanz kann denselben Refresh-Token einen Augenblick frueher eingeloest haben.
                // Dann liegt ihr neues Token schon da, und die Absage hier ist kein Grund, neu anzumelden.
                var current = await ReadTokenAsync(service.OAuthServiceId, tenantId, tenantName, cancellationToken);
                if (current.RefreshToken != token.RefreshToken && current.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                {
                    logger.LogWarning("Refreshing the login of tenant {TenantId} at '{ConnectionName}' was rejected ({StatusCode}), but another instance had already stored a fresh token; that one is used.",
                        tenantId, service.UniqueConnectionName, (int)response.StatusCode);
                    return current.AccessToken;
                }

                logger.LogError("Refreshing the login of tenant {TenantId} at '{ConnectionName}' was rejected with {StatusCode}: {Body}. If this is invalid_grant, the tenant has to sign in again.",
                    tenantId, service.UniqueConnectionName, (int)response.StatusCode, body);
                throw Unavailable($"The login at '{service.UniqueConnectionName}' could not be renewed ({(int)response.StatusCode}).");
            }

            var fresh = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                        ?? throw Unavailable($"'{service.UniqueConnectionName}' answered the refresh with an empty body.");
            var stored = new TranslatedTokenResponse
            {
                AccessToken = fresh.AccessToken,
                TokenType = fresh.TokenType,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(fresh.ExpiresIn),
                // Nicht jeder Anbieter tauscht den Refresh-Token aus. Kommt keiner, gilt der alte weiter.
                RefreshToken = string.IsNullOrEmpty(fresh.RefreshToken) ? token.RefreshToken : fresh.RefreshToken,
                Scope = fresh.Scope ?? token.Scope
            };

            await StoreTokenAsync(service.OAuthServiceId, tenantId, tenantName, stored, cancellationToken);
            return stored.AccessToken;
        }

        private async Task<TranslatedTokenResponse> ReadTokenAsync(int serviceId, int tenantId, string tenantName,
            CancellationToken cancellationToken)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var login = await db.Set<TExternalOAuthServiceTenantLogin>().AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(l => l.OAuthServiceId == serviceId && l.TenantId == tenantId, cancellationToken);
            if (login == null || login.Revoked || string.IsNullOrEmpty(login.Token))
            {
                logger.LogError("Tenant {TenantId} has no {State} OAuth login at external service {ServiceId}; the tenant has to sign in.",
                    tenantId, login == null ? "stored" : "active", serviceId);
                throw Unavailable("The tenant is not signed in at this provider.");
            }

            var raw = JsonHelper.FromJsonString<TranslatedTokenResponse>(login.Token, SerializationTypingMode.StaticTyping);
            return new TranslatedTokenResponse
            {
                AccessToken = securityRepository.Decrypt(raw.AccessToken, tenantName),
                RefreshToken = string.IsNullOrEmpty(raw.RefreshToken) ? raw.RefreshToken : securityRepository.Decrypt(raw.RefreshToken, tenantName),
                ExpiresAt = raw.ExpiresAt,
                Scope = raw.Scope,
                TokenType = raw.TokenType
            };
        }

        private async Task StoreTokenAsync(int serviceId, int tenantId, string tenantName, TranslatedTokenResponse token,
            CancellationToken cancellationToken)
        {
            var encrypted = new TranslatedTokenResponse
            {
                AccessToken = securityRepository.Encrypt(token.AccessToken, tenantName),
                RefreshToken = string.IsNullOrEmpty(token.RefreshToken) ? token.RefreshToken : securityRepository.Encrypt(token.RefreshToken, tenantName),
                ExpiresAt = token.ExpiresAt,
                Scope = token.Scope,
                TokenType = token.TokenType
            };

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var login = await db.Set<TExternalOAuthServiceTenantLogin>().IgnoreQueryFilters()
                .FirstAsync(l => l.OAuthServiceId == serviceId && l.TenantId == tenantId, cancellationToken);
            login.Token = JsonHelper.ToJson(encrypted, SerializationTypingMode.StaticTyping);
            login.Revoked = false;
            await db.SaveChangesAsync(cancellationToken);
        }

        private async Task<string> TenantNameAsync(TContext db, int tenantId, CancellationToken cancellationToken)
        {
            var name = await db.Set<TTenant>().AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.TenantId == tenantId).Select(t => t.TenantName).FirstOrDefaultAsync(cancellationToken);
            if (name == null)
            {
                logger.LogError("Tenant {TenantId} does not exist; no provider credentials can be read or stored for it.", tenantId);
                throw Unavailable($"Tenant {tenantId} does not exist.");
            }

            return name;
        }

        private static TenantPaymentException Unavailable(string message)
            => new(PaymentErrorCodes.OwnAccessUnavailable, message);
    }
}
