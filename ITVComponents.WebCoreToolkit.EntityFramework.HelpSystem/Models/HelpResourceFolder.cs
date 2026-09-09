using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>
    /// A folder in the resource library - purely for organising the admin view. The tree can be as deep as
    /// the librarian wants it to be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ordner beruehren die Aufloesung NICHT.</b> Eine Ressource wird aus dem Hilfe-Inhalt weiterhin ueber
    /// ihren flachen, global eindeutigen Namen angesprochen (<c>resource:{Name}</c>); wo sie im Baum liegt,
    /// interessiert dabei niemanden. Genau deshalb ist das hier eine EIGENE Entitaet und kein
    /// <c>Kind = Folder</c> an der Ressource: sonst teilten sich Ordner- und Ressourcennamen einen
    /// Eindeutigkeits-Index (ein Ordner "logos" und eine Ressource "logos" schloessen sich aus), und jeder
    /// Lesepfad muesste Ordner kuenftig herausfiltern - vergisst man eine Stelle, taucht ein Ordner als
    /// "Ressource ohne Datei" auf.
    /// </para>
    /// <para>
    /// Eine Sortierung gibt es bewusst nicht: die Anzeige ordnet nach Namen. Was hier fehlt, muss auch
    /// niemand pflegen.
    /// </para>
    /// </remarks>
    public class HelpResourceFolder
    {
        [Key]
        public int HelpResourceFolderId { get; set; }

        /// <summary>
        /// Stable identity of this folder beyond the local database. The configuration export compares folders by
        /// this tag instead of by their path, so a renamed or moved folder arrives as exactly that - over a path
        /// key it would be indistinguishable from a different folder and land next to the one it replaced.
        /// </summary>
        /// <remarks>
        /// Derselbe Mechanismus wie <c>NavigationMenu.RefTag</c>: wo der Wert fehlt, wird beim naechsten Export
        /// eine GUID nachgetragen. Weiterhin kein Eindeutigkeits-Index - die Vergabe liegt beim Code.
        /// </remarks>
        [MaxLength(1024)]
        public string? RefTag { get; set; }

        /// <summary>Parent folder; null for a folder at the root.</summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// The folder's name.
        /// </summary>
        /// <remarks>
        /// Kein Eindeutigkeits-Index: er muesste (ParentId, Name) umfassen, und ueber eine NULL-Spalte
        /// verhalten sich SQL Server und PostgreSQL dabei verschieden (dort waeren zwei gleichnamige
        /// Wurzelordner erlaubt, hier nicht). Fuer eine reine Ordnungsstruktur ist das die falsche Stelle
        /// fuer Datenbank-Akrobatik - der Handler weist einen doppelten Namen mit einer verstaendlichen
        /// Meldung ab.
        /// </remarks>
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [ForeignKey(nameof(ParentId))]
        public virtual HelpResourceFolder? Parent { get; set; }

        public virtual ICollection<HelpResourceFolder> Children { get; set; } = new List<HelpResourceFolder>();

        /// <summary>The resources filed in this folder.</summary>
        public virtual ICollection<HelpResource> Resources { get; set; } = new List<HelpResource>();
    }
}
