using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    [Index(nameof(TenantName),IsUnique=true,Name="IX_UniqueTenant")]
    [Index(nameof(TenantNameLower),IsUnique=true,Name="IX_UniqueTenantLower")]
    public class Tenant : ITenantIdentity
    {
        [Key]
        public int TenantId { get; set; }

        [Required,MaxLength(150)]
        public string TenantName { get; set; }

        /// <summary>
        /// Der kleingeschriebene Mandantenname - <b>berechnet und gespeichert</b>, nur zum Vergleichen und
        /// fuer den eindeutigen Schluessel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Warum eine eigene Spalte:</b> Mandantennamen werden ueberall ohne Ruecksicht auf Gross- und
        /// Kleinschreibung verglichen - auf SQL Server erledigt das die Kollation, auf <b>PostgreSQL
        /// nicht</b>. Ein <c>lower()</c> in der Bedingung loest das zwar, kann dann aber
        /// <c>IX_UniqueTenant</c> nicht mehr benutzen. Ein funktionaler Index waere die Alternative -
        /// den kennt EF Core weder als Attribut noch ueber die Fluent-API, er stuende also in keinem
        /// Modell und beim Anwender poppte nie eine Migration dafuer auf. Diese Spalte steht im Modell,
        /// also erzwingt sie eine.
        /// </para>
        /// <para>
        /// <b>Sie ist auch EINDEUTIG</b> (<c>IX_UniqueTenantLower</c>), und das ist der wichtigere Teil:
        /// ohne sie liesse die Datenbank <c>Laden1</c> und <c>laden1</c> nebeneinander zu, waehrend der
        /// Code beide als denselben Mandanten liest - und dann trifft <c>First(...)</c> irgendeinen von
        /// beiden. <b>Achtung bei Bestandsdaten:</b> genau deshalb scheitert die Migration auf einer
        /// Datenbank, in der solche Paare schon existieren. Das ist beabsichtigt; sie sind vorher
        /// aufzuloesen.
        /// </para>
        /// <para>
        /// Das Berechnungs-SQL steht je Datenbank im zustaendigen <c>ColumnsSyntaxHelper</c> - auf
        /// PostgreSQL <b>zwingend mit <c>stored: true</c></b>, sonst bricht schon das Erzeugen der
        /// Migration ab.
        /// </para>
        /// <para>
        /// <b>Wer ohne echte Datenbank arbeitet, muss sie selbst fuellen.</b> SQLite und der
        /// InMemory-Provider kennen die Berechnung nicht; weil die Spalte <c>Computed</c> ist, schickt EF
        /// einen im Code gesetzten Wert beim Einfuegen aber gar nicht erst mit. Der Datensatz scheitert
        /// dann an <c>NOT NULL</c> bzw. an „Required properties are missing". Der Ausweg ist beides
        /// zusammen: im Testkontext
        /// <c>Property(t =&gt; t.TenantNameLower).ValueGeneratedNever()</c> setzen <b>und</b> den Wert beim
        /// Anlegen mitgeben - siehe <c>FeatureActivationTest</c>.
        /// </para>
        /// </remarks>
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(150),Required,ExcludeFromDictionary]
        public string TenantNameLower { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        [MaxLength(125),ExcludeFromDictionary]
        public string TenantPassword { get; set; }

        [MaxLength(1024)]
        public string TimeZone { get; set; }

        public int? TenantTypeId { get; set; }

        public bool? TenantDirty { get; set; }

        [ForeignKey(nameof(TenantTypeId))]
        public virtual TenantType? TenantType { get; set; }
    }
}
