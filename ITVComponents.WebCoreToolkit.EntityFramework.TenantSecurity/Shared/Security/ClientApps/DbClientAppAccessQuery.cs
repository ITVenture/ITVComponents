using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// Beide Namensraeume: die generischen Basisklassen stehen in .Models.Base, der konkrete Tenant in
// .Models. Die Basisklassen selbst kommen ohne dieses zweite using aus, weil .Models.Base in .Models
// EINGEBETTET ist und die Namenssuche den umschliessenden Namensraum mitnimmt - diese Datei liegt
// anderswo und hat den Vorteil nicht.
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models.Base;
using ITVComponents.WebCoreToolkit.Security.ClientApps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ClientApps
{
    /// <summary>
    /// Schlaegt ClientApp-Zugaenge in der Datenbank nach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kommt mit <b>18</b> Typparametern aus statt mit den 45 des <c>DbSecurityRepository</c>: sie braucht
    /// nur die Zugangs-Entitaet und deren Navigationen, und der Kontext wird ueber
    /// <see cref="DbContext.Set{TEntity}()"/> angesprochen statt ueber eine Kontext-Schnittstelle.
    /// </para>
    /// <para>
    /// Arbeitet auf einem <b>eigenen Kontext je Vorgang</b> (<see cref="IDbContextFactory{TContext}"/>):
    /// die Anmeldung laeuft je Anfrage und darf sich nicht mit dem Kontext eines laufenden Vorgangs
    /// verschraenken.
    /// </para>
    /// </remarks>
    public class DbClientAppAccessQuery<TContext, TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess> : IClientAppAccessQuery
        where TContext : DbContext
        where TRole : Role<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TPermission : Permission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TUserRole : UserRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TRolePermission : RolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TTenantUser : TenantUser<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TAppPermission : AppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TAppPermissionSet : AppPermissionSet<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet>
        where TClientAppPermission : ClientAppPermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TClientApp : ClientApp<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TClientAppAccess : ClientAppAccess<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole, TAppPermission, TAppPermissionSet, TClientAppPermission, TClientApp, TClientAppAccess>
        where TTenant : Tenant
        where TRoleRole : RoleRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRole : GlobalRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGlobalRolePermission : GlobalRolePermission<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
        where TGRoleLRole : GRoleLRole<TTenant, TUserId, TUser, TRole, TPermission, TUserRole, TRolePermission, TTenantUser, TRoleRole, TGlobalRole, TGlobalRolePermission, TGRoleLRole>
    {
        private readonly IDbContextFactory<TContext> dbFactory;
        private readonly ILogger logger;

        public DbClientAppAccessQuery(IDbContextFactory<TContext> dbFactory, ILoggerFactory loggerFactory)
        {
            this.dbFactory = dbFactory;
            logger = loggerFactory.CreateLogger("DbClientAppAccessQuery");
        }

        /// <inheritdoc/>
        public async Task<ClientAppAccessInfo> ResolveAsync(string clientKey, string label,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(clientKey) || string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var now = DateTime.UtcNow;

            // Ein Zug ueber zwei indizierte Felder (UQ_ClientAppKey, UQ_ClientAppAccess). Die
            // Gueltigkeit steht in derselben Bedingung: ein widerrufener, abgelaufener oder zu einer
            // abgeschalteten Anwendung gehoerender Zugang wird gar nicht erst herausgegeben.
            var hit = await db.Set<TClientAppAccess>()
                .Where(n => n.Label == label
                            && n.ClientApp.ClientKey == clientKey
                            && n.ClientApp.Enabled
                            && n.RevokedUtc == null
                            && (n.ExpiresUtc == null || n.ExpiresUtc > now))
                .Select(n => new ClientAppAccessInfo(
                    n.ClientAppAccessId,
                    n.Label,
                    n.SecretHash,
                    n.ClientApp.Tenant.TenantName,
                    n.TenantUserId == null))
                .FirstOrDefaultAsync(ct);

            if (hit == null)
            {
                logger.LogDebug(
                    "No valid client-app access for client-key {ClientKey} and label {Label}.",
                    clientKey, label);
                return null;
            }

            if (string.IsNullOrEmpty(hit.SecretHash))
            {
                // Ein Zugang ohne Geheimnis kann sich nicht anmelden. Das ist kein Angriff, sondern eine
                // halbfertige Kopplung - und ohne diese Zeile sucht jemand an der falschen Stelle.
                logger.LogWarning(
                    "Client-app access {Label} has no secret hash - the pairing was never completed. It cannot authenticate.",
                    label);
                return null;
            }

            return hit;
        }

        /// <inheritdoc/>
        public async Task MarkUsedAsync(int clientAppAccessId, CancellationToken ct = default)
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                await db.Set<TClientAppAccess>()
                    .Where(n => n.ClientAppAccessId == clientAppAccessId)
                    .ExecuteUpdateAsync(s => s.SetProperty(n => n.LastUsedUtc, DateTime.UtcNow), ct);
            }
            catch (Exception ex)
            {
                // Der Vermerk darf die Anmeldung nicht scheitern lassen - das Geraet hat sich korrekt
                // ausgewiesen. Verschweigen darf man es trotzdem nicht: bleibt LastUsedUtc stehen, sieht
                // ein arbeitendes Geraet wie ein stillgelegtes aus.
                logger.LogError(ex,
                    "Could not record the last use of client-app access {AccessId}.", clientAppAccessId);
            }
        }
    }
}
