using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Models.ExternalServiceConnect;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.ExternalOAuthServices
{
    /// <summary>
    /// Die festen Zugangsdaten einer Zugangsdaten-Zeile (<c>ExternalOAuthServiceTenantLogin.CredentialJson</c>) - was
    /// je Anmeldetyp das Konto ausmacht, im Gegensatz zur App, die an der Definition steht.
    /// </summary>
    public sealed class ExternalServiceCredential
    {
        /// <summary>Benutzer bzw. Client des Kontos (Basic, Password-Grant, Client-Credentials); sonst null.</summary>
        public string User { get; set; }

        /// <summary>Das Geheimnis: Passwort, Client-Secret, API-Schluessel oder Bearer-Token.</summary>
        public string Secret { get; set; }
    }

    /// <summary>
    /// Ver- und entschluesselt <see cref="ExternalServiceCredential"/> und legt sie je Anmeldetyp auf eine
    /// <see cref="ExternalServiceConnection"/>.
    /// </summary>
    /// <remarks>
    /// Die Verbindung behaelt ihre Form (<c>ClientId</c>/<c>ClientSecret</c>), darum aendern sich Token-Dienst, Handler
    /// und OAuth-Rueckruf nicht. Was in diesen Feldern steht, entscheidet der Typ - siehe <see cref="ApplyTo"/>.
    /// </remarks>
    public static class ExternalServiceCredentials
    {
        /// <summary>Verschluesselt die Zugangsdaten als Ganzes, mit dem Schluessel des Besitzers der Zeile.</summary>
        public static string Protect(ExternalServiceCredential credential, Func<string, string> encrypt)
            => credential == null ? null : encrypt(JsonSerializer.Serialize(credential));

        /// <summary>Ob eine Zeile Zugangsdaten traegt.</summary>
        public static bool HasCredentials(string credentialJson) => !string.IsNullOrEmpty(credentialJson);

        /// <summary>
        /// Waehlt die Zeile, aus der Zugangsdaten UND Token eines Dienstes kommen.
        /// </summary>
        /// <param name="candidates">die eigene bzw. geerbten Zeilen, die naechste zuerst (flach: hoechstens die eigene)</param>
        /// <param name="platform">liefert die Plattform-Zeile, wenn die Definition sie zulaesst, sonst null; nur bei Bedarf gerufen</param>
        /// <param name="type">der Anmeldetyp des Dienstes</param>
        /// <param name="credentialJson">liest die Zugangsdaten einer Zeile</param>
        /// <param name="subject">fuer das Protokoll: welcher Dienst, fuer welchen Mandanten</param>
        /// <returns>die Zeile, oder null, wenn es keine gibt</returns>
        /// <remarks>
        /// Bei Authorization-Code ist die Zeile das Login selbst: die naechste gilt, sonst die der Plattform. Bei festen
        /// Zugangsdaten gilt eine Zeile OHNE Zugangsdaten nicht als Treffer - typisch ein Token-Puffer aus 4.x, der nach dem
        /// Umzug die Plattform- oder Besitzer-Zeile verdecken wuerde (Issue External Services 01). Gesucht wird dann weiter: den
        /// Baum hinauf, dann die Plattform. Token und Zugangsdaten bleiben aus derselben, der gefundenen Zeile. Hat keine Zeile
        /// Zugangsdaten, gilt wie bisher die naechste (dort liegt dann wenigstens das Token), mit einer Warnung.
        /// </remarks>
        public static TLogin PickCredentialRow<TLogin>(IReadOnlyList<TLogin> candidates, Func<TLogin> platform,
            ExternalServiceAuthenticationType type, Func<TLogin, string> credentialJson, string subject)
            where TLogin : class
        {
            if (type == ExternalServiceAuthenticationType.OAuthAuthorizationFlow)
            {
                return candidates.FirstOrDefault() ?? platform();
            }

            var withCredentials = candidates.FirstOrDefault(c => HasCredentials(credentialJson(c)));
            if (withCredentials != null)
            {
                if (!ReferenceEquals(withCredentials, candidates[0]))
                {
                    LogEnvironment.LogEvent($"The nearest row of {subject} has no credentials and was skipped; a row further up the tenant tree is used. Remove the row without credentials (a token cache from 4.x) to silence this.", LogSeverity.Warning);
                }

                return withCredentials;
            }

            var platformRow = platform();
            if (platformRow != null && HasCredentials(credentialJson(platformRow)))
            {
                if (candidates.Count != 0)
                {
                    LogEnvironment.LogEvent($"The row(s) of {subject} have no credentials and were skipped; the platform row is used. Remove the row without credentials (a token cache from 4.x) to silence this.", LogSeverity.Warning);
                }

                return platformRow;
            }

            LogEnvironment.LogEvent($"No credentials found for {subject}: no row of the tenant, of its parents or (if allowed) of the platform carries any. The request is sent without credentials. Set them under 'Credentials'.", LogSeverity.Warning);
            return candidates.FirstOrDefault() ?? platformRow;
        }

        /// <summary>Entschluesselt gespeicherte Zugangsdaten; null, wenn keine gespeichert sind.</summary>
        public static ExternalServiceCredential Unprotect(string stored, Func<string, string> decrypt)
            => string.IsNullOrEmpty(stored) ? null : JsonSerializer.Deserialize<ExternalServiceCredential>(decrypt(stored));

        /// <summary>
        /// Leert, was an der Verbindung zum KONTO gehoert, und legt dann die Zugangsdaten der Zeile darauf (falls es eine
        /// gibt). Seit §93 gilt nur noch die Zeile - ein Rest an der Definition (etwa von einer aelteren Maske gesetzt)
        /// darf nicht still weiterwirken.
        /// </summary>
        public static void Assign(ExternalServiceConnection connection, ExternalServiceCredential credential)
        {
            if (connection == null)
            {
                return;
            }

            switch (connection.AuthenticationType)
            {
                case ExternalServiceAuthenticationType.OAuthAuthorizationFlow:
                    return;
                case ExternalServiceAuthenticationType.ApiKey:
                case ExternalServiceAuthenticationType.BearerToken:
                    connection.ClientSecret = null;
                    break;
                default:
                    connection.ClientId = null;
                    connection.ClientSecret = null;
                    break;
            }

            ApplyTo(connection, credential);
        }

        /// <summary>
        /// Legt die Zugangsdaten auf die Verbindung - nur dorthin, wo der Typ das Konto erwartet.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>Authorization-Code: nichts. <c>ClientId</c>/<c>ClientSecret</c> sind die App (Definition); das Konto ist
        /// das Token.</item>
        /// <item>ApiKey: nur das Geheimnis. <c>ClientId</c> ist der Header-Name und gehoert zur Definition.</item>
        /// <item>BearerToken: nur das Geheimnis.</item>
        /// <item>Basic, Password-Grant, Client-Credentials: Benutzer bzw. Client und Geheimnis.</item>
        /// </list>
        /// Ein leeres Feld der Zugangsdaten laesst das der Definition stehen.
        /// </remarks>
        public static void ApplyTo(ExternalServiceConnection connection, ExternalServiceCredential credential)
        {
            if (connection == null || credential == null)
            {
                return;
            }

            switch (connection.AuthenticationType)
            {
                case ExternalServiceAuthenticationType.OAuthAuthorizationFlow:
                    return;
                case ExternalServiceAuthenticationType.ApiKey:
                case ExternalServiceAuthenticationType.BearerToken:
                    if (!string.IsNullOrEmpty(credential.Secret))
                    {
                        connection.ClientSecret = credential.Secret;
                    }

                    return;
                default:
                    if (!string.IsNullOrEmpty(credential.User))
                    {
                        connection.ClientId = credential.User;
                    }

                    if (!string.IsNullOrEmpty(credential.Secret))
                    {
                        connection.ClientSecret = credential.Secret;
                    }

                    return;
            }
        }
    }
}
