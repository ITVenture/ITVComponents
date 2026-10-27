using System;
using System.Text.Json;
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

        /// <summary>Entschluesselt gespeicherte Zugangsdaten; null, wenn keine gespeichert sind.</summary>
        public static ExternalServiceCredential Unprotect(string stored, Func<string, string> decrypt)
            => string.IsNullOrEmpty(stored) ? null : JsonSerializer.Deserialize<ExternalServiceCredential>(decrypt(stored));

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
