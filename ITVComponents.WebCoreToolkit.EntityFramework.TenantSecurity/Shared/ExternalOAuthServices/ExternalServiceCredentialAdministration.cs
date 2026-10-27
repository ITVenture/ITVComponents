using System;
using System.Collections.Generic;
using System.Linq;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices
{
    /// <summary>Eine Zugangsdaten-Zeile, wie die Admin-Masken sie zeigen - nie mit Geheimnis.</summary>
    public sealed class ExternalServiceCredentialRowInfo
    {
        public int ExternalOAuthServiceTenantLoginId { get; set; }

        public int OAuthServiceId { get; set; }

        /// <summary>Der Besitzer; null = die Plattform.</summary>
        public int? TenantId { get; set; }

        public string TenantName { get; set; }

        public bool Revoked { get; set; }

        public bool HasCredentials { get; set; }

        public bool HasToken { get; set; }

        public bool Inheritable { get; set; }

        /// <summary>Ob der aktuelle Benutzer die Zeile aendern darf.</summary>
        public bool Editable { get; set; }
    }

    /// <summary>
    /// Die Regeln fuer das Pflegen von Zugangsdaten-Zeilen - an EINER Stelle, damit die Blazor- und die Telerik-Maske
    /// dasselbe tun (Leitfaden §91/§93).
    /// </summary>
    /// <remarks>
    /// Liest und schreibt ohne Abfragefilter, mit ausdruecklichem Besitzer. Ob der Aufrufer den Dienst sehen darf und ob er
    /// Plattform-Zugangsdaten setzen darf, prueft der Aufrufer (er kennt seine Rechte und seinen Bereich).
    /// </remarks>
    public static class ExternalServiceCredentialAdministration
    {
        /// <summary>Ob der Typ feste Zugangsdaten hat (alle ausser Authorization-Code, das nur ein Token kennt).</summary>
        public static bool HasFixedCredentials(ExternalServiceAuthenticationType type)
            => type != ExternalServiceAuthenticationType.OAuthAuthorizationFlow;

        /// <summary>Ob das Konto dieses Typs einen Benutzer bzw. Client neben dem Geheimnis hat.</summary>
        public static bool AccountHasUser(ExternalServiceAuthenticationType type)
            => type is ExternalServiceAuthenticationType.Basic or ExternalServiceAuthenticationType.OAuthPasswordFlow
                or ExternalServiceAuthenticationType.OAuthClientCredentialsFlow
                or ExternalServiceAuthenticationType.OAuthBasicClientCredentialsFlow;

        /// <summary>
        /// Ob <c>ClientId</c> zur DEFINITION gehoert: die App (Authorization-Code) oder der Header-Name (ApiKey).
        /// </summary>
        public static bool DefinitionCarriesClient(ExternalServiceAuthenticationType type)
            => type is ExternalServiceAuthenticationType.OAuthAuthorizationFlow or ExternalServiceAuthenticationType.ApiKey;

        /// <summary>Ob der Kontext ein Mandantenbaum ist - nur dann lassen sich Zugangsdaten weitergeben.</summary>
        public static bool SupportsInheritance(Type contextType)
            => contextType.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition().Name.StartsWith("IHierarchyTenantContext", StringComparison.Ordinal));

        /// <summary>
        /// Die Zeilen eines Dienstes: fuer einen Sysadmin alle (auch die der Plattform), sonst nur die des aktuellen Mandanten.
        /// </summary>
        public static List<ExternalServiceCredentialRowInfo> List<TTenant, TService, TState, TLogin>(DbContext db, int serviceId,
            int? currentTenantId, bool sysAdmin)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
            => (from l in db.Set<TLogin>().IgnoreQueryFilters().AsNoTracking()
                where l.OAuthServiceId == serviceId && (sysAdmin || (l.TenantId != null && l.TenantId == currentTenantId))
                join t in db.Set<TTenant>().IgnoreQueryFilters().AsNoTracking() on l.TenantId equals (int?)t.TenantId into tj
                from tn in tj.DefaultIfEmpty()
                orderby l.TenantId
                select new ExternalServiceCredentialRowInfo
                {
                    ExternalOAuthServiceTenantLoginId = l.ExternalOAuthServiceTenantLoginId,
                    OAuthServiceId = l.OAuthServiceId,
                    TenantId = l.TenantId,
                    TenantName = tn != null ? tn.DisplayName : null,
                    Revoked = l.Revoked,
                    HasCredentials = l.CredentialJson != null && l.CredentialJson != "",
                    HasToken = l.Token != null && l.Token != "" && l.Token != "{}",
                    Inheritable = l.Inheritable,
                    Editable = sysAdmin || (l.TenantId != null && l.TenantId == currentTenantId)
                }).ToList();

        /// <summary>
        /// Speichert die festen Zugangsdaten eines Besitzers (null = Plattform) fuer einen Dienst. Leeres Geheimnis = das
        /// gespeicherte behalten; geaenderte Zugangsdaten verwerfen das Token.
        /// </summary>
        /// <returns>null bei Erfolg, sonst der Grund der Ablehnung</returns>
        public static string Save<TTenant, TService, TState, TLogin>(DbContext db, ISecurityRepository security, TService service,
            int? ownerTenantId, string user, string secret, bool inheritable)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
        {
            if (!HasFixedCredentials(service.AuthenticationType))
            {
                return "This service connects through the OAuth login - there are no fixed credentials to store.";
            }

            string ownerName = null;
            if (ownerTenantId != null)
            {
                ownerName = db.Set<TTenant>().IgnoreQueryFilters().Where(t => t.TenantId == ownerTenantId)
                    .Select(t => t.TenantName).FirstOrDefault();
                if (ownerName == null)
                {
                    return "The tenant of these credentials does not exist.";
                }
            }

            Func<string, string> encrypt = v => ownerName == null ? v.Encrypt() : security.Encrypt(v, ownerName);
            Func<string, string> decrypt = v => ownerName == null ? v.Decrypt() : security.Decrypt(v, ownerName);
            var row = db.Set<TLogin>().IgnoreQueryFilters()
                .FirstOrDefault(n => n.OAuthServiceId == service.OAuthServiceId && n.TenantId == ownerTenantId);
            var stored = ExternalServiceCredentials.Unprotect(row?.CredentialJson, decrypt);
            var credential = new ExternalServiceCredential
            {
                User = AccountHasUser(service.AuthenticationType) ? user?.Trim() : null,
                Secret = string.IsNullOrEmpty(secret) ? stored?.Secret : secret
            };
            if (string.IsNullOrEmpty(credential.Secret))
            {
                return "A secret is required.";
            }

            var changed = stored == null || stored.User != credential.User || stored.Secret != credential.Secret;
            if (row == null)
            {
                // Activator statt new(): die Einschraenkung muesste sonst bis in jede Registrierung durchgereicht werden.
                row = Activator.CreateInstance<TLogin>();
                row.OAuthServiceId = service.OAuthServiceId;
                row.TenantId = ownerTenantId;
                row.Token = "{}";
                db.Set<TLogin>().Add(row);
            }

            row.CredentialJson = ExternalServiceCredentials.Protect(credential, encrypt);
            row.Inheritable = ownerTenantId != null && inheritable && SupportsInheritance(db.GetType());
            row.Revoked = false;
            if (changed)
            {
                // Ein Token, das mit den alten Zugangsdaten geholt wurde, gehoert nicht zu den neuen.
                row.Token = "{}";
            }

            db.SaveChanges();
            return null;
        }

        /// <summary>Die Zeile, wenn der Aufrufer sie aendern darf (die eigene; ein Sysadmin jede), sonst null.</summary>
        public static TLogin FindEditable<TTenant, TService, TState, TLogin>(DbContext db, int loginId, int? currentTenantId, bool sysAdmin)
            where TTenant : Tenant
            where TService : ExternalOAuthService<TTenant, TService, TState, TLogin>
            where TState : ExternalOAuthServiceState<TTenant, TService, TState, TLogin>
            where TLogin : ExternalOAuthServiceTenantLogin<TTenant, TService, TState, TLogin>
            => db.Set<TLogin>().IgnoreQueryFilters().Include(n => n.OAuthService)
                .FirstOrDefault(n => n.ExternalOAuthServiceTenantLoginId == loginId
                                     && (sysAdmin || (n.TenantId != null && n.TenantId == currentTenantId)));
    }
}
