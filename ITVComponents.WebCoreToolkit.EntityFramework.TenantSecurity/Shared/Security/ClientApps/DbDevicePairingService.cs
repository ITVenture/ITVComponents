using System;
using System.Collections.Generic;
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
        where TClientAppTemplate : ClientAppTemplate<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppTemplate>
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
        public async Task<PairingRequest> StartAsync(string clientKey, string typeKey, string deviceLabel,
            CancellationToken ct = default)
        {
            var o = settings.Value;
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            int? tenantId = null;
            int? clientAppId = null;
            int templateId;
            string target;
            if (!string.IsNullOrWhiteSpace(clientKey))
            {
                // Der fruehere Weg: das Geraet legt die Anwendung selbst fest, sie bringt den Mandanten mit.
                var app = await db.Set<TClientApp>()
                    .Where(n => n.ClientKey == clientKey && n.Enabled)
                    .Select(n => new { n.ClientAppId, n.TenantId, n.ClientName, n.ClientAppTemplateId })
                    .FirstOrDefaultAsync(ct);
                if (app == null)
                {
                    logger.LogWarning(
                        "Pairing was requested for client-key {ClientKey}, which is unknown or switched off.",
                        clientKey);
                    return null;
                }

                tenantId = app.TenantId;
                clientAppId = app.ClientAppId;
                templateId = app.ClientAppTemplateId;
                target = $"application {app.ClientName} (tenant {app.TenantId})";
            }
            else if (!string.IsNullOrWhiteSpace(typeKey))
            {
                // Der Regelfall: das Geraet sagt nur, was es ist. Anwendung und Mandant waehlt, wer bestaetigt.
                var key = typeKey.Trim();
                var template = await db.Set<TClientAppTemplate>()
                    .Where(n => n.TypeKey == key)
                    .Select(n => new { n.ClientAppTemplateId, n.Name })
                    .FirstOrDefaultAsync(ct);
                if (template == null)
                {
                    logger.LogWarning(
                        "Pairing was requested for type-key {TypeKey}, which no client-app template carries.",
                        key);
                    return null;
                }

                templateId = template.ClientAppTemplateId;
                target = $"type {key} (template {template.Name})";
            }
            else
            {
                logger.LogWarning("Pairing was requested with neither a client-key nor a type-key; refused.");
                return null;
            }

            // Der Geraetecode ist das Geheimnis dieses Vorgangs - in der Ablage steht nur sein Hash.
            var deviceCode = SecretHasher.CreateSecret();
            var now = DateTime.UtcNow;

            var pairing = new TDevicePairing
            {
                TenantId = tenantId,
                ClientAppId = clientAppId,
                ClientAppTemplateId = templateId,
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
                "Pairing {UserCode} opened for {Target}, expires {Expires}.",
                pairing.UserCode, target, pairing.ExpiresUtc);

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

            string templateName = null;
            IReadOnlyList<PairingPermissionSet> templateSets = Array.Empty<PairingPermissionSet>();
            if (pairing.ClientAppTemplateId != null)
            {
                templateName = await db.Set<TClientAppTemplate>()
                    .Where(n => n.ClientAppTemplateId == pairing.ClientAppTemplateId)
                    .Select(n => n.Name)
                    .FirstOrDefaultAsync(ct);
                templateSets = await db.Set<TAppPermissionSet>()
                    .Where(n => n.ClientAppTemplateId == pairing.ClientAppTemplateId)
                    .OrderBy(n => n.Name)
                    .Select(n => new PairingPermissionSet(n.AppPermissionSetId, n.Name))
                    .ToListAsync(ct);
            }

            // Die Auswahl: hat das Geraet seine Anwendung festgelegt, ist es genau die; sonst die Anwendungen
            // seines Templates im Mandanten dessen, der gerade bestaetigt.
            var apps = db.Set<TClientApp>().AsQueryable();
            if (pairing.ClientAppId != null)
            {
                apps = apps.Where(n => n.ClientAppId == pairing.ClientAppId);
            }
            else
            {
                var tenant = (permissionScope.PermissionPrefix ?? "").ToLower();
                apps = apps.Where(n => n.ClientAppTemplateId == pairing.ClientAppTemplateId
                                       && n.Enabled
                                       && n.Tenant.TenantNameLower == tenant);
            }

            var candidates = await LoadCandidatesAsync(apps, pairing.DeviceLabel, ct);

            // Genau das, was der Bestaetiger sehen muss: welche Rechtebuendel er gleich erteilt.
            var fixedApp = pairing.ClientAppId == null ? null : candidates.FirstOrDefault();
            return new PairingPreview(true, fixedApp?.ClientAppName, pairing.DeviceLabel,
                fixedApp?.PermissionSets ?? Array.Empty<string>(),
                pairing.ClientAppId, pairing.ClientAppTemplateId, templateName, candidates, templateSets);
        }

        /// <summary>
        /// Die Anwendungen, an die gekoppelt werden kann, samt ihrer Buendel und der Instanz, die schon an
        /// ihnen haengt.
        /// </summary>
        private async Task<IReadOnlyList<PairingCandidate>> LoadCandidatesAsync(IQueryable<TClientApp> apps,
            string deviceLabel, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var rows = await apps
                .OrderBy(n => n.ClientName)
                .Select(n => new
                {
                    n.ClientAppId,
                    n.ClientName,
                    Sets = n.AppPermissions.Select(p => p.PermissionSet.Name).OrderBy(p => p).ToList(),
                    Active = n.Accesses
                        .Where(a => a.TenantUserId == null && a.RevokedUtc == null
                                    && (a.ExpiresUtc == null || a.ExpiresUtc > now))
                        .Select(a => new { a.DeviceLabel, a.Label })
                        .ToList()
                })
                .ToListAsync(ct);

            return rows.Select(n => new PairingCandidate(
                    n.ClientAppId,
                    n.ClientName,
                    n.Sets,
                    n.Active.Count == 0 ? null : string.Join(", ", n.Active.Select(a => a.DeviceLabel ?? a.Label)),
                    // Dasselbe Geraet darf neu koppeln - es ersetzt dann seinen Zugang.
                    n.Active.All(a => a.DeviceLabel == deviceLabel)))
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<PairingConfirmation> ConfirmAsync(string userCode, int? clientAppId,
            CancellationToken ct = default)
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

            int targetAppId;
            if (pairing.ClientAppId != null)
            {
                // Das Geraet hat seine Anwendung selbst festgelegt - eine andere Wahl waere eine Umleitung.
                if (clientAppId != null && clientAppId != pairing.ClientAppId)
                {
                    logger.LogWarning(
                        "Pairing {UserCode} was requested for application {RequestedApp}, but confirmed for {ChosenApp}. Refused.",
                        code, pairing.ClientAppId, clientAppId);
                    return new PairingConfirmation(false, "This device asked for another application.");
                }

                targetAppId = pairing.ClientAppId.Value;
            }
            else if (clientAppId == null)
            {
                logger.LogWarning("Pairing {UserCode} was confirmed without choosing an application. Refused.", code);
                return new PairingConfirmation(false, "Choose the application this device is paired to.");
            }
            else
            {
                targetAppId = clientAppId.Value;
            }

            var app = await db.Set<TClientApp>()
                .Where(n => n.ClientAppId == targetAppId)
                .Select(n => new { n.ClientAppId, n.TenantId, n.ClientName, n.ClientAppTemplateId, n.Enabled, n.Tenant.TenantName })
                .FirstOrDefaultAsync(ct);
            if (app == null)
            {
                logger.LogWarning("Pairing {UserCode} was confirmed for application {AppId}, which does not exist. Refused.",
                    code, targetAppId);
                return new PairingConfirmation(false, "The chosen application does not exist.");
            }

            // Die Mandantengrenze: der Bestaetiger muss im Mandanten der Anwendung stehen. Ohne diese
            // Pruefung koennte ein Administrator eines fremden Mandanten ein Geraet freischalten - oder eines
            // an die Anwendung eines fremden Mandanten haengen.
            var currentTenant = permissionScope.PermissionPrefix;
            var appTenant = app.TenantName;
            if (!string.Equals(currentTenant, appTenant, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Pairing {UserCode} targets application {AppId} of tenant {AppTenant}, but was confirmed from {CurrentTenant}. Refused.",
                    code, app.ClientAppId, appTenant, currentTenant);
                return new PairingConfirmation(false, "This pairing belongs to another tenant.");
            }

            if (!app.Enabled)
            {
                logger.LogWarning("Pairing {UserCode} was confirmed for application {App}, which is switched off. Refused.",
                    code, app.ClientName);
                return new PairingConfirmation(false, "The chosen application is switched off.");
            }

            if (pairing.ClientAppTemplateId != null && pairing.ClientAppTemplateId != app.ClientAppTemplateId)
            {
                logger.LogWarning(
                    "Pairing {UserCode} is a device of template {DeviceTemplate}, but application {App} belongs to template {AppTemplate}. Refused.",
                    code, pairing.ClientAppTemplateId, app.ClientName, app.ClientAppTemplateId);
                return new PairingConfirmation(false, "The chosen application is of another type than the device.");
            }

            var now = DateTime.UtcNow;

            // Hoechstens EINE aktive Instanz je Anwendung: der Agent meldet sich unter dem ClientKey der
            // Anwendung am Dienst-Verteiler an, und die Geraete-Rollen haengen an der Anwendung. Eine zweite
            // Kasse an derselben Anwendung wuerde dort abgewiesen - oder, wenn die erste gerade nicht
            // verbunden ist, mit deren Geraeten arbeiten. Nur dasselbe Geraet (gleicher Name) darf neu
            // koppeln; das ersetzt unten seinen Zugang. Gegen zwei GLEICHZEITIGE Bestaetigungen fuer
            // dieselbe Anwendung schuetzt das nicht - dafuer muesste die Datenbank es halten.
            var others = await db.Set<TClientAppAccess>()
                .Where(n => n.ClientAppId == app.ClientAppId && n.TenantUserId == null && n.RevokedUtc == null
                            && (n.ExpiresUtc == null || n.ExpiresUtc > now)
                            && n.DeviceLabel != pairing.DeviceLabel)
                .Select(n => n.DeviceLabel ?? n.Label)
                .ToListAsync(ct);
            if (others.Count != 0)
            {
                logger.LogWarning(
                    "Pairing {UserCode} was refused: application {App} already has an active instance ({Instances}).",
                    code, app.ClientName, string.Join(", ", others));
                return new PairingConfirmation(false,
                    $"The application already has an active instance ({string.Join(", ", others)}). Revoke it under 'Connected instances' first.");
            }

            // Ein Geraet, das ERNEUT koppelt, ersetzt seinen bisherigen Zugang. Ohne das sammelt sich je
            // Kopplung ein weiterer an, und - das ist der Punkt - der alte Schluessel bleibt gueltig: ein
            // Geraet, das ihn noch gespeichert hat, meldet sich damit weiter an. Ein Konsument kam so auf
            // acht Zugaenge fuer EINE Kasse; im Protokoll standen 180 Fehlanmeldungen mit einem laengst
            // ersetzten Label, waehrend der frische Zugang parallel funktionierte. Die Suche begann
            // dadurch an der voellig falschen Stelle.
            // Nur Maschinenzugaenge (TenantUserId == null): eine Delegation gehoert einem Menschen und
            // wird nicht von der naechsten Geraetekopplung abgeraeumt.
            // Geladen statt ExecuteUpdate: es sind null bis wenige Zeilen, und so laeuft derselbe Weg auch im
            // InMemory-Provider der Tests. Gespeichert wird zusammen mit dem neuen Zugang.
            var predecessors = await db.Set<TClientAppAccess>()
                .Where(n => n.ClientAppId == app.ClientAppId && n.DeviceLabel == pairing.DeviceLabel
                            && n.TenantUserId == null && n.RevokedUtc == null)
                .ToListAsync(ct);
            predecessors.ForEach(n => n.RevokedUtc = now);
            var replaced = predecessors.Count;
            if (replaced != 0)
            {
                logger.LogInformation(
                    "Pairing {UserCode}: {Count} earlier access(es) of device {DeviceLabel} were revoked - a device carries exactly one key.",
                    code, replaced, pairing.DeviceLabel);
            }

            // Der Zugang entsteht hier - OHNE Geheimnis. Das entsteht erst beim Abholen; wuerde es hier
            // erzeugt, muesste es bis dahin irgendwo im Klartext liegen.
            var access = new TClientAppAccess
            {
                ClientAppId = app.ClientAppId,
                TenantUserId = null,
                Label = BuildLabel(app.ClientName),
                DeviceLabel = pairing.DeviceLabel,
                CreatedUtc = now
            };
            db.Set<TClientAppAccess>().Add(access);
            await db.SaveChangesAsync(ct);

            pairing.TenantId = app.TenantId;
            pairing.ClientAppId = app.ClientAppId;
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
                $"{clientKey}.{access.Label}.{secret}", o.PollIntervalSeconds, clientKey);
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
                .Where(n => n.Tenant.TenantNameLower == (tenantName ?? "").ToLower() && userIds.Contains(n.UserId))
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
        /// <para>
        /// Der Zufallsanteil ist Pflicht, nicht Zierde: <c>UQ_ClientAppAccess</c> ist systemweit eindeutig und
        /// gilt auch fuer widerrufene Zugaenge - jede Neukopplung derselben Kasse braucht ein neues Label, und
        /// "Kasse 1" gibt es in jedem Mandanten einmal.
        /// </para>
        /// <para>
        /// Der Praefix ist der Name der ANWENDUNG, nicht der des Rechners: im Protokoll und unter den
        /// verbundenen Instanzen soll man sehen, welche Kasse sich meldet. Ohne Punkte - das Label steht
        /// mitten im Schluessel <c>&lt;ClientKey&gt;.&lt;Label&gt;.&lt;Geheimnis&gt;</c>.
        /// </para>
        /// </remarks>
        private static string BuildLabel(string clientAppName)
        {
            var prefix = string.IsNullOrWhiteSpace(clientAppName) ? "device" : clientAppName.Trim();
            // Nur ASCII: der Schluessel reist als Header (X-Api-Key), und "Kasse Büro" ist ein Anwendungsname,
            // der dort nichts verloren hat.
            prefix = new string(prefix.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_').ToArray());
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
