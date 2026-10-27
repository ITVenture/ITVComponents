namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    /// <summary>Eine Zugangsdaten-Zeile in der Kendo-Maske - nie mit gespeichertem Geheimnis.</summary>
    public class ExternalServiceCredentialViewModel
    {
        public int ExternalOAuthServiceTenantLoginId { get; set; }

        public int OAuthServiceId { get; set; }

        /// <summary>Die Zeile der Plattform statt der des eigenen Mandanten (nur beim Anlegen, nur Sysadmin bei globalem Dienst).</summary>
        public bool Platform { get; set; }

        public string OwnerName { get; set; }

        public bool HasCredentials { get; set; }

        public bool HasToken { get; set; }

        public bool Revoked { get; set; }

        /// <summary>An Untermandanten ohne eigene Zugangsdaten weitergeben (nur im Baum).</summary>
        public bool Inheritable { get; set; }

        public bool Editable { get; set; }

        /// <summary>Benutzer bzw. Client des Kontos; wird nie zurueckgelesen.</summary>
        public string User { get; set; }

        /// <summary>Das Geheimnis; leer beim Aendern = behalten. Wird nie zurueckgelesen.</summary>
        public string Secret { get; set; }
    }
}
