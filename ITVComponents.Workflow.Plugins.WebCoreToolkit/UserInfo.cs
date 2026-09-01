using System;
using System.Collections.Generic;

namespace ITVComponents.Workflow.Plugins.WebCoreToolkit
{
    /// <summary>
    /// Was ein Vorgang ueber einen Benutzer wissen kann: die flachen Angaben unter stabilen Namen und
    /// die konkreten Datensaetze daneben.
    /// </summary>
    /// <remarks>
    /// <b>Die flachen Felder sind eine Verschmelzung mit Vorrang: der Mitarbeiter schlaegt den
    /// Benutzer.</b> Ohne diese Regel bedeutete <c>EMail</c> je nach Ausprägung etwas anderes - am
    /// Mitarbeiter steht die Geschaeftsadresse, am Identity-Benutzer die Anmeldeadresse, und der
    /// Basic-Benutzer hat gar keine. Was nirgends steht, bleibt null.
    /// <para>
    /// Die flachen Felder sind der Teil, auf den sich ein Feld-Pfad in einer Definition verlassen darf:
    /// <c>kunde.EMail</c> funktioniert in jeder Ausprägung. Wer mehr braucht, greift ueber
    /// <see cref="User"/>, <see cref="TenantUser"/> oder <see cref="Employee"/> durch - und nimmt die
    /// Bindung an die konkrete Ausprägung dann bewusst in Kauf.
    /// </para>
    /// </remarks>
    public sealed class UserInfo
    {
        /// <summary>
        /// Wurde ueberhaupt etwas gefunden? Nur dann tragen die uebrigen Felder etwas.
        /// </summary>
        /// <remarks>
        /// Steht nur dann auf false, wenn der Handler mit <c>AllowMissing</c> laeuft - sonst ist „nicht
        /// gefunden" ein Fehler und kein Ergebnis.
        /// </remarks>
        public bool Found { get; set; }

        /// <summary>Die Id des Benutzers - je nach Ausprägung eine Zahl oder eine Zeichenkette.</summary>
        public object UserId { get; set; }

        /// <summary>Der Anmeldename.</summary>
        public string UserName { get; set; }

        /// <summary>Vor- und Nachname, sonst der Anmeldename - was man einem Menschen zeigt.</summary>
        public string DisplayName { get; set; }

        /// <summary>Die Mailadresse (Mitarbeiter vor Benutzer vor Rechnungsprofil).</summary>
        public string EMail { get; set; }

        /// <summary>
        /// Die Telefonnummer. Kommt vom Mitarbeiter, sonst vom Rechnungsprofil - der Benutzer-Datensatz
        /// selbst traegt keine.
        /// </summary>
        public string PhoneNumber { get; set; }

        /// <summary>
        /// Der Vorname - aus dem Mitarbeiter-Datensatz, sonst aus dem persoenlichen Rechnungsprofil.
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// Der Nachname - aus dem Mitarbeiter-Datensatz, sonst aus dem persoenlichen Rechnungsprofil.
        /// </summary>
        public string LastName { get; set; }

        /// <summary>Der Mandant, in dem der Benutzer gefunden wurde.</summary>
        public int? TenantId { get; set; }

        /// <summary>Die Mandanten-Zuordnung, falls es eine gibt.</summary>
        public int? TenantUserId { get; set; }

        /// <summary>Ob die Mandanten-Zuordnung aktiv ist.</summary>
        public bool? Enabled { get; set; }

        /// <summary>Der Mitarbeiter-Datensatz, falls es einen gibt.</summary>
        public int? EmployeeId { get; set; }

        /// <summary>Der Stand der Einladung, als Text (der Wert der Aufzaehlung).</summary>
        public string InvitationStatus { get; set; }

        /// <summary>
        /// Das persoenliche Rechnungsprofil, als dessen Eigentuemer dieser Benutzer eingetragen ist -
        /// falls es eines gibt.
        /// </summary>
        /// <remarks>
        /// Der Weg fuer den <b>Mandanten-Eigentuemer</b>: wer einen Mandanten anlegt, wird im
        /// Rechnungsprofil als <c>OwnerUser</c> hinterlegt, und ein Mitarbeiter-Datensatz entsteht dabei
        /// nicht. Ohne diesen Weg blieben Vor- und Nachname genau bei der Person leer, die den Mandanten
        /// besitzt. Nur Profile vom Typ <c>Personal</c> - bei einem Firmenprofil beschreiben die
        /// Namensfelder nicht diesen Benutzer.
        /// </remarks>
        public int? BillingProfileId { get; set; }

        /// <summary>
        /// Die benutzerdefinierten Eigenschaften, nach Namen. Ein Dictionary ist fuer den Member-Zugriff
        /// ein ganz normaler Traeger - <c>kunde.Properties.Kostenstelle</c> loest also auf.
        /// </summary>
        public IDictionary<string, object> Properties { get; }
            = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Der konkrete Benutzer-Datensatz, oder null.</summary>
        public object User { get; set; }

        /// <summary>Der konkrete Mandanten-Benutzer-Datensatz, oder null.</summary>
        public object TenantUser { get; set; }

        /// <summary>Der konkrete Mitarbeiter-Datensatz, oder null.</summary>
        public object Employee { get; set; }

        /// <summary>Der konkrete Rechnungsprofil-Datensatz (Typ <c>Personal</c>), oder null.</summary>
        public object BillingProfile { get; set; }

        /// <summary>Nennt den beschriebenen Benutzer - fuer Protokoll und Fehlermeldungen.</summary>
        /// <returns>eine kurze Beschreibung dieses Ergebnisses</returns>
        public override string ToString()
        {
            return Found
                ? $"{DisplayName ?? UserName ?? "(no name)"} [user {UserId}]"
                : "(no user found)";
        }
    }
}
