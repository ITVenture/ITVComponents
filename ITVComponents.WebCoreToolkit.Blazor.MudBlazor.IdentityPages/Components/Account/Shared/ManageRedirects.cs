using System;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages.Components.Account.Shared
{
    /// <summary>
    /// Die Rueckspruenge der Konto-Seiten auf die Endpunkte, die ein Authentifizierungs-Cookie schreiben.
    ///
    /// Die Seiten rendern interaktiv und koennen darum selbst kein Cookie setzen. Sie erledigen ihre Aenderung
    /// auf dem Circuit und rufen dann eine dieser Methoden: voller Seitenaufbau ueber den Endpunkt, Cookie
    /// geschrieben, Weiterleitung zurueck auf die Seite - inklusive Statusmeldung, die den Neuaufbau ueberlebt,
    /// weil sie in der Adresse steht.
    ///
    /// Hier gebuendelt, damit die Adressen nicht in sechs Seiten einzeln zusammengebaut werden; laufen sie
    /// auseinander, faellt das erst am Host auf.
    /// </summary>
    internal static class ManageRedirects
    {
        private const string RefreshSignInEndpoint = "Account/Manage/RefreshSignIn";
        private const string SignOutEndpoint = "Account/Manage/SignOutSession";
        private const string ForgetBrowserEndpoint = "Account/Manage/ForgetBrowser";

        /// <summary>
        /// Cookie neu ausstellen und auf <paramref name="localPath"/> zurueckkehren. Nach allem, was den
        /// Security-Stamp aendert (Passwort, 2FA, externe Logins) zwingend - sonst meldet der Stamp-Validator
        /// den Benutzer beim naechsten Intervall ab.
        /// </summary>
        public static void RefreshSignIn(NavigationManager nav, string localPath, string? status = null)
            => Go(nav, RefreshSignInEndpoint, localPath, status);

        /// <summary>Sitzung beenden und weiterleiten - nachdem das Konto geloescht wurde.</summary>
        public static void SignOut(NavigationManager nav, string localPath = "/")
            => Go(nav, SignOutEndpoint, localPath, null);

        /// <summary>"Diesen Browser merken" fuer die Zwei-Faktor-Anmeldung vergessen und zurueckkehren.</summary>
        public static void ForgetBrowser(NavigationManager nav, string localPath, string? status = null)
            => Go(nav, ForgetBrowserEndpoint, localPath, status);

        private static void Go(NavigationManager nav, string endpoint, string localPath, string? status)
        {
            var target = localPath.StartsWith('/') ? localPath : "/" + localPath;
            if (!string.IsNullOrEmpty(status))
            {
                target = $"{target}?status={Uri.EscapeDataString(status)}";
            }

            nav.NavigateTo($"{endpoint}?returnUrl={Uri.EscapeDataString(target)}", forceLoad: true);
        }
    }
}
