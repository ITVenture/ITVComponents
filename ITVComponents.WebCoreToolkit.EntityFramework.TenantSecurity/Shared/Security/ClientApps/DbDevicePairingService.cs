using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Security.DevicePairing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps
{
    /// <summary>
    /// Der Kopplungs-Ablauf auf der Datenbank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Arbeitet bewusst ohne Mandantenfilter.</b> <c>StartAsync</c> und <c>PollAsync</c> laufen anonym,
    /// da gibt es keinen Mandanten - die Grenze zieht der Dienst selbst: beim Anlegen ueber die Anwendung,
    /// beim Bestaetigen ueber den Mandanten des Benutzers, beim Abholen ueber den Geraetecode, der selbst
    /// das Geheimnis ist. Deshalb haengt die Kopplungstabelle an <c>IDevicePairingContext</c> und nicht am
    /// gefilterten <c>ISecurityContext</c>.
    /// </para>
    /// </remarks>
    public class DbDevicePairingService<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TDevicePairing, TClientAppTemplate> : IDevicePairingService
        where TContext : DbContext
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>
        where TClientAppAccess : ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>, new()
        where TDevicePairing : DevicePairing<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess, TClientAppTemplate>, new()
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly IGlobalSettings<DevicePairingOptions> settings;
        private readonly IPermissionScope permissionScope;
        private readonly IContextUserProvider userProvider;
        private readonly ISecurityRepository securityRepository;
        private readonly ILogger logger;

        public DbDevicePairingService(IDbContextFactory<TContext> dbFactory,
            IGlobalSettings<DevicePairingOptions> settings,
            IPermissionScope permissionScope,
            IContextUserProvider userProvider,
            ISecurityRepository securityRepository,
            ILoggerFactory loggerFactory)
        {
            this.dbFactory = dbFactory;
            this.settings = settings;
            this.permissionScope = permissionScope;
            this.userProvider = userProvider;
            this.securityRepository = securityRepository;
            logger = loggerFactory.CreateLogger("DbDevicePairingService");
        }

        /// <inheritdoc/>
        public async Task<PairingRequest> StartAsync(string clientKey, string deviceLabel,
            CancellationToken ct = default)
        {
            var o = settings.Value;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var app = await db.Set<TClientApp>()
                .Where(n => n.ClientKey == clientKey && n.Enabled)
                .Select(n => new { n.ClientAppId, n.TenantId, n.ClientName })
                .FirstOrDefaultAsync(ct);
            if (app == null)
            {
                logger.LogWarning(
                    "Pairing was requested for client-key {ClientKey}, which is unknown or switched off.",
                    clientKey);
                return null;
            }

            // Der Geraetecode ist das Geheimnis dieses Vorgangs - in der Ablage steht nur sein Hash.
            var deviceCode = SecretHasher.CreateSecret();
            var now = DateTime.UtcNow;

            var pairing = new TDevicePairing
            {
                TenantId = app.TenantId,
                ClientAppId = app.ClientAppId,
                DeviceCodeHash = SecretHasher.Hash(deviceCode),
                UserCode = UserCodeGenerator.Create(o.UserCodeLength),
                DeviceLabel = Trim(deviceLabel, 200),
                CreatedUtc = now,
                ExpiresUtc = now.AddMinutes(o.LifetimeMinutes),
                State = DevicePairingState.Pending
            };

            db.Set<TDevicePairing>().Add(pairing);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Pairing {UserCode} opened for application {App} (tenant {TenantId}), expires {Expires}.",
                pairing.UserCode, app.ClientName, app.TenantId, pairing.ExpiresUtc);

            return new PairingRequest(deviceCode, pairing.UserCode, pairing.ExpiresUtc, o.PollIntervalSeconds);
        }

        /// <inheritdoc/>
        public async Task<PairingPreview> DescribeAsync(string userCode, CancellationToken ct = default)
        {
            var code = UserCodeGenerator.Normalize(userCode);
            if (code == null)
            {
                return new PairingPreview(false, null, null, Array.Empty<string>());
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var pairing = await FindOpenAsync(db, code, ct);
            if (pairing == null)
            {
                return new PairingPreview(false, null, null, Array.Empty<string>());
            }

            // Genau das, was der Bestaetiger sehen muss: welche Rechtebuendel er gleich erteilt.
            var sets = await db.Set<TClientAppPermission>()
                .Where(n => n.ClientAppId == pairing.ClientAppId)
                .Select(n => n.PermissionSet.Name)
                .OrderBy(n => n)
                .ToListAsync(ct);

            var appName = await db.Set<TClientApp>()
                .Where(n => n.ClientAppId == pairing.ClientAppId)
                .Select(n => n.ClientName)
                .FirstOrDefaultAsync(ct);

            return new PairingPreview(true, appName, pairing.DeviceLabel, sets);
        }

        /// <inheritdoc/>
        public async Task<PairingConfirmation> ConfirmAsync(string userCode, CancellationToken ct = default)
        {
            var code = UserCodeGenerator.Normalize(userCode);
            if (code == null)
            {
                return new PairingConfirmation(false, "The code is not readable.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var pairing = await FindOpenAsync(db, code, ct);
            if (pairing == null)
            {
                logger.LogWarning("Pairing {UserCode} was confirmed, but no open pairing carries that code.", code);
                return new PairingConfirmation(false, "No open pairing for this code.");
            }

            // Die Mandantengrenze: der Bestaetiger muss im Mandanten der Anwendung stehen. Ohne diese
            // Pruefung koennte ein Administrator eines fremden Mandanten ein Geraet freischalten.
            var currentTenant = permissionScope.PermissionPrefix;
            var appTenant = await db.Set<TClientApp>()
                .Where(n => n.ClientAppId == pairing.ClientAppId)
                .Select(n => n.Tenant.TenantName)
                .FirstOrDefaultAsync(ct);
            if (!string.Equals(currentTenant, appTenant, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Pairing {UserCode} belongs to tenant {AppTenant}, but was confirmed from {CurrentTenant}. Refused.",
                    code, appTenant, currentTenant);
                return new PairingConfirmation(false, "This pairing belongs to another tenant.");
            }

            var now = DateTime.UtcNow;

            // Der Zugang entsteht hier - OHNE Geheimnis. Das entsteht erst beim Abholen; wuerde es hier
            // erzeugt, muesste es bis dahin irgendwo im Klartext liegen.
            var access = new TClientAppAccess
            {
                ClientAppId = pairing.ClientAppId,
                TenantUserId = null,
                Label = BuildLabel(pairing.DeviceLabel),
                DeviceLabel = pairing.DeviceLabel,
                CreatedUtc = now
            };
            db.Set<TClientAppAccess>().Add(access);
            await db.SaveChangesAsync(ct);

            pairing.State = DevicePairingState.Confirmed;
            pairing.ConfirmedUtc = now;
            pairing.ConfirmedByTenantUserId = await ResolveConfirmingTenantUserAsync(db, appTenant, ct);
            pairing.ClientAppAccessId = access.ClientAppAccessId;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Pairing {UserCode} confirmed; access {Label} created for application {AppId}.",
                code, access.Label, pairing.ClientAppId);
            return new PairingConfirmation(true, null);
        }

        /// <inheritdoc/>
        public async Task<PairingResult> PollAsync(string deviceCode, CancellationToken ct = default)
        {
            var o = settings.Value;
            if (string.IsNullOrWhiteSpace(deviceCode))
            {
                return new PairingResult(DevicePairingState.Denied, null, o.PollIntervalSeconds);
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var now = DateTime.UtcNow;

            // Der Geraetecode steht nur als Hash in der Ablage - es laesst sich also nicht danach suchen.
            // Die Kandidaten sind die offenen Vorgaenge; das ist eine sehr kurze Liste, weil sie nach
            // Minuten verfallen.
            var candidates = await db.Set<TDevicePairing>()
                .Where(n => n.State == DevicePairingState.Pending || n.State == DevicePairingState.Confirmed)
                .ToListAsync(ct);
            var pairing = candidates.FirstOrDefault(n => SecretHasher.Verify(deviceCode, n.DeviceCodeHash));

            if (pairing == null)
            {
                logger.LogDebug("A device polled with a code that matches no open pairing.");
                return new PairingResult(DevicePairingState.Denied, null, o.PollIntervalSeconds);
            }

            pairing.PollCount++;
            pairing.LastPollUtc = now;

            if (pairing.ExpiresUtc <= now)
            {
                pairing.State = DevicePairingState.Expired;
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Pairing {UserCode} expired before it was confirmed.", pairing.UserCode);
                return new PairingResult(DevicePairingState.Expired, null, o.PollIntervalSeconds);
            }

            if (pairing.PollCount > o.MaxPolls)
            {
                // Die zweite Haelfte der Bremse. Wer so oft fragt, wartet nicht - er sucht.
                pairing.State = DevicePairingState.Denied;
                await db.SaveChangesAsync(ct);
                logger.LogWarning(
                    "Pairing {UserCode} was polled {Count} times and is now refused.", pairing.UserCode, pairing.PollCount);
                return new PairingResult(DevicePairingState.Denied, null, o.PollIntervalSeconds);
            }

            if (pairing.State == DevicePairingState.Pending)
            {
                await db.SaveChangesAsync(ct);
                return new PairingResult(DevicePairingState.Pending, null, o.PollIntervalSeconds);
            }

            // Bestaetigt und noch nicht abgeholt: JETZT entsteht das Geheimnis, und nur hier.
            var secret = SecretHasher.CreateSecret();
            var access = await db.Set<TClientAppAccess>()
                .FirstOrDefaultAsync(n => n.ClientAppAccessId == pairing.ClientAppAccessId.Value, ct);
            if (access == null)
            {
                // Sollte nicht vorkommen - der Zugang entsteht beim Bestaetigen. Wenn doch, ist der
                // Vorgang kaputt und darf nicht stillschweigend weiterlaufen.
                pairing.State = DevicePairingState.Denied;
                await db.SaveChangesAsync(ct);
                logger.LogError(
                    "Pairing {UserCode} is confirmed but its access {AccessId} is gone. The pairing is refused.",
                    pairing.UserCode, pairing.ClientAppAccessId);
                return new PairingResult(DevicePairingState.Denied, null, o.PollIntervalSeconds);
            }

            var clientKey = await db.Set<TClientApp>()
                .Where(n => n.ClientAppId == pairing.ClientAppId)
                .Select(n => n.ClientKey)
                .FirstAsync(ct);

            access.SecretHash = SecretHasher.Hash(secret);
            pairing.State = DevicePairingState.Delivered;
            pairing.SecretDeliveredUtc = now;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Pairing {UserCode} delivered the secret for access {Label}. It will not be handed out again.",
                pairing.UserCode, access.Label);

            return new PairingResult(DevicePairingState.Delivered,
                $"{clientKey}.{access.Label}.{secret}", o.PollIntervalSeconds);
        }

        /// <inheritdoc/>
        public async Task<bool> RevokeAsync(string label, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var affected = await db.Set<TClientAppAccess>()
                .Where(n => n.Label == label && n.RevokedUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.RevokedUtc, DateTime.UtcNow), ct);

            if (affected == 0)
            {
                logger.LogWarning("Revoking access {Label} changed nothing - unknown or already revoked.", label);
                return false;
            }

            logger.LogInformation("Access {Label} was revoked.", label);
            return true;
        }

        /// <summary>
        /// Wer bestaetigt hat - fuer die Nachvollziehbarkeit, nicht fuer die Rechte.
        /// </summary>
        /// <remarks>
        /// Bleibt null, wenn sich der Benutzer nicht zuordnen laesst. Das ist ausdruecklich KEIN Grund,
        /// die Bestaetigung scheitern zu lassen: die Rechtepruefung ist am Endpunkt schon passiert, und
        /// ein fehlender Eintrag im Protokoll darf keine Kopplung verhindern.
        /// </remarks>
        private async Task<int?> ResolveConfirmingTenantUserAsync(TContext db, string tenantName,
            CancellationToken ct)
        {
            var name = userProvider?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(name))
            {
                logger.LogWarning(
                    "The confirming user could not be identified; the pairing is recorded without one.");
                return null;
            }

            // Ueber das SecurityRepository, weil nur es aus dem Principal die Benutzer-Id macht - die
            // Ausformung von TUserId (int oder string) und die Frage, welches Anspruchsfeld den Namen
            // traegt, gehoeren dorthin und nicht hierher.
            var userIds = securityRepository.GetUserIds<TUserId>(new[] { name },
                userProvider.User.Identity.AuthenticationType).ToArray();
            if (userIds.Length == 0)
            {
                logger.LogWarning(
                    "The confirming user {User} could not be resolved to a user-id; the pairing is recorded without one.",
                    name);
                return null;
            }

            var match = await db.Set<TTenantUser>()
                .Where(n => n.Tenant.TenantName == tenantName && userIds.Contains(n.UserId))
                .Select(n => (int?)n.TenantUserId)
                .FirstOrDefaultAsync(ct);
            if (match == null)
            {
                logger.LogWarning(
                    "The confirming user {User} has no tenant-user entry in {Tenant}; the pairing is recorded without one.",
                    name, tenantName);
            }

            return match;
        }

        private async Task<TDevicePairing> FindOpenAsync(TContext db, string userCode, CancellationToken ct)
            => await db.Set<TDevicePairing>()
                .FirstOrDefaultAsync(n => n.UserCode == userCode
                                          && n.State == DevicePairingState.Pending
                                          && n.ExpiresUtc > DateTime.UtcNow, ct);

        /// <summary>
        /// Baut den systemweit eindeutigen Bezeichner eines Zugangs.
        /// </summary>
        /// <remarks>
        /// Der Zufallsanteil ist Pflicht, nicht Zierde: <c>UQ_ClientAppAccess</c> ist systemweit eindeutig,
        /// und "Terminal-01" gibt es in jeder Filiale einmal.
        /// </remarks>
        private static string BuildLabel(string deviceLabel)
        {
            var prefix = string.IsNullOrWhiteSpace(deviceLabel) ? "device" : deviceLabel.Trim();
            prefix = new string(prefix.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (prefix.Length > 80)
            {
                prefix = prefix[..80];
            }

            if (prefix.Length == 0)
            {
                prefix = "device";
            }

            return $"{prefix}-{Guid.NewGuid():N}";
        }

        private static string Trim(string value, int max)
            => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
    }
}
