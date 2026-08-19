using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.TreeShared.Helpers
{
    /// <summary>
    /// Die Namen, ueber die der <b>providerneutrale</b> Code und die provider-eigenen Syntax-Helper
    /// zueinander finden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hier stehen <b>zwei verschiedene Arten</b> von Namen, und sie duerfen nicht verwechselt werden:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///   <b>Datenbank-Objekte</b> (Views, Funktionen). An ihnen haengen EF-Entitaeten bzw.
    ///   <c>[DbFunction]</c>-Deklarationen - und zwar <b>ueber den Namen</b>. Ein Provider muss ein
    ///   Objekt <b>genau so</b> nennen, sonst findet EF es nicht. Das ist zugleich die gute Nachricht:
    ///   stimmt der Name, braucht es fuer diese Wege keine Zeile providerspezifisches C#.
    ///   </item>
    ///   <item>
    ///   <b>Schluessel im Methoden-Verzeichnis</b> (<c>IContextModelBuilderOptions.ConfigureMethod</c> /
    ///   <c>GetMethod</c>). Das sind <b>keine</b> Datenbank-Objekte, sondern Haken, an denen ein Provider
    ///   seine Umsetzung aufhaengt. Sie werden gebraucht, wo sich der Zugriff nicht ueber einen blossen
    ///   Namen ausdruecken laesst - etwa weil T-SQL eine Prozedur aufruft und PostgreSQL eine Funktion,
    ///   oder weil die Abfrage selbst anders aussehen muss.
    ///   </item>
    /// </list>
    /// <para>
    /// Faustregel fuer neuen Code: <b>steht irgendwo SQL, das nur auf einem Provider laeuft, gehoert es
    /// hinter einen Schluessel von hier</b> - nicht in den geteilten Code. Fehlt die Umsetzung, meldet
    /// <c>GetMethod</c> null, und der Aufrufer wirft mit klarer Meldung; das ist gewollt und viel besser
    /// als eine Abfrage, die erst in der Datenbank auffaellt.
    /// </para>
    /// </remarks>
    public static class GlobalDbObjectNaming
    {
        // ---------------------------------------------------------------------------------------
        // Datenbank-Objekte: Views. Entitaeten sind ueber diese Namen darauf abgebildet.
        // ---------------------------------------------------------------------------------------

        /// <summary>View: der Mandanten-Baum nach OBEN (Blatt -> Wurzel).</summary>
        public const string UpwardsTenantTreeView = "UpwardsTenantTree";

        /// <summary>View: der Mandanten-Baum nach UNTEN (Wurzel -> Blaetter).</summary>
        public const string DownwardsTenantTreeView = "DownwardsTenantTree";

        /// <summary>View: der Zugriffs-Baum eines Benutzers (beide Richtungen zusammengefuehrt).</summary>
        public const string UserAccessTree = "TenantAccessTree";

        /// <summary>View: der Zugriffs-Baum nach unten. Keine Verwendung im Toolkit - Schnittstelle fuer Konsumenten.</summary>
        public const string UserAccessTreeDownView = "TenantAccessTreeDown";

        /// <summary>View: der Zugriffs-Baum nach oben. Keine Verwendung im Toolkit - Schnittstelle fuer Konsumenten.</summary>
        public const string UserAccessTreeUpView = "TenantAccessTreeUp";

        /// <summary>View: der Rollen-Baum nach oben.</summary>
        public const string UpwardsRoleTreeView = "UpwardsRoleTree";

        /// <summary>View: der Rollen-Baum nach unten.</summary>
        public const string DownwardsRoleTreeView = "DownwardsRoleTree";

        // ---------------------------------------------------------------------------------------
        // Datenbank-Objekte: Funktionen. EF bindet sie ueber [DbFunction] an DIESE Namen.
        // ---------------------------------------------------------------------------------------

        /// <summary>Funktion: Rollen-Baum nach oben fuer eine Benutzer-Id.</summary>
        public const string UpwardsRoleTreeForIdFunction = "GetUpwardsRoleTreeForId";

        /// <summary>Funktion: Rollen-Baum nach oben fuer eine Liste von Kennzeichen.</summary>
        public const string UpwardsRoleTreeForLabelsFunction = "GetUpwardsRoleTreeForLabels";

        /// <summary>Funktion: wie <see cref="UpwardsRoleTreeForIdFunction"/>, eingeschraenkt auf ein Blatt.</summary>
        public const string UpwardsRoleTreeForIdByLeafFunction = "GetUpwardsRoleTreeForIdByLeafId";

        /// <summary>Funktion: wie <see cref="UpwardsRoleTreeForLabelsFunction"/>, eingeschraenkt auf ein Blatt.</summary>
        public const string UpwardsRoleTreeForLabelsByLeafFunction = "GetUpwardsRoleTreeForLabelsByLeafId";

        /// <summary>Funktion: der Rollen-Baum nach oben (allgemeine Form).</summary>
        public const string UpwardsRoleTreeFunction = "GetUpwardsRoleTree";

        /// <summary>Funktion: die wirksamen Rollen eines Mandanten-Benutzers (Tabellenwert).</summary>
        public const string EffectiveTenantUserRolesFunction = "GetEffectiveTenantUserRoles";

        // ---------------------------------------------------------------------------------------
        // Datenbank-Objekte: Prozeduren. Der NAME kann ueber Datenbanken hinweg gleich bleiben, die
        // AUFRUFART nicht - deshalb laeuft der Zugriff darauf zusaetzlich ueber das Methoden-Verzeichnis.
        // ---------------------------------------------------------------------------------------

        /// <summary>Prozedur: der Rollen-Baum nach unten fuer einen Benutzer.</summary>
        public const string DownwardsRoleTreeProcName = "GetDownwardsRoleTreeProc";

        /// <summary>Prozedur: Kind-Mandanten mit bestimmten Berechtigungen (Mandant ueber den Namen).</summary>
        public const string ChildTenantsWithPermsProcName = "GetChildTenantsWithPermsProc";

        /// <summary>Prozedur: der Rollen-Baum nach unten, Blickwinkel ueber die Mandanten-Id.</summary>
        public const string DownwardsRoleTreeByVpIdProcName = "GetDownwardsRoleTreeByVpIdProc";

        /// <summary>Prozedur: Kind-Mandanten mit Berechtigungen, Blickwinkel ueber die Mandanten-Id.</summary>
        public const string ChildTenantsWithPermsByVpIdProcName = "GetChildTenantsWithPermsByVpIdProc";

        // ---------------------------------------------------------------------------------------
        // Schluessel im Methoden-Verzeichnis. KEINE Datenbank-Objekte - siehe Anmerkung an der Klasse.
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Methode: Kind-Mandanten, in denen ein Benutzer bestimmte Berechtigungen hat. Vier
        /// Ueberladungen (Id/Kennzeichen x Mandantenname/-Id).
        /// </summary>
        /// <remarks>
        /// Der Konstantenname traegt aus historischen Gruenden „Proc"; der Wert ist ein
        /// Verzeichnis-Schluessel und nicht der Name der T-SQL-Prozedur dahinter.
        /// </remarks>
        public const string ChildTenantsWithProc = "ChildTenantsWith";

        /// <summary>
        /// Methode: der Rollen-Baum nach UNTEN fuer einen Benutzer. Auf SQL Server eine Prozedur;
        /// PostgreSQL kennt keine Prozedur, die eine Ergebnismenge liefert, und braucht dort eine
        /// Funktion - genau deshalb laeuft der Zugriff ueber das Verzeichnis und nicht ueber den Namen.
        /// </summary>
        public const string DownwardsTenantUserRolesMethod = "DownwardsTenantUserRoles";

        /// <summary>
        /// Methode: je Benutzer die <b>naechstgelegenen</b> Zeilen des Rollen-Baums nach oben, gesucht
        /// ueber Kennzeichen.
        /// </summary>
        /// <remarks>
        /// Die Umsetzung muss ein <b>komponierbares</b> <c>IQueryable</c> liefern und darf nicht
        /// materialisieren: der Aufrufer haengt einen Join an, und der gehoert in dieselbe Abfrage.
        /// Wird hier ein Array zurueckgegeben, zerfaellt der Zugriff in zwei Datenbankrunden - also
        /// genau das, was die Zusammenfassung auf EINEN Baum-Durchlauf beseitigt hat.
        /// </remarks>
        public const string ClosestUpwardsRoleTreeByLabelsMethod = "ClosestUpwardsRoleTreeByLabels";
    }
}
