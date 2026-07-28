using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Options
{
    /// <summary>
    /// Konfigurationssaetze fuer die Workflow-<b>Umgebungen</b> einer Host-Anwendung, die mehrere
    /// getrennte Workflow-Datenbestaende (z.B. verschiedene Tools mit je eigenem Store) verwaltet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Geladen ueber <see cref="IHierarchySettings{TSettings}"/> (scoped vor global) unter dem Schluessel
    /// <c>WorkflowEnvironmentSettings</c>. <b>Ist keine solche Einstellung hinterlegt (oder
    /// <see cref="Environments"/> leer), laeuft alles wie bisher</b> - die Views nutzen die einzelne, per
    /// DI registrierte Umgebung, ohne Umgebungs-Auswahl. Erst mit konfigurierten Umgebungen erscheint im
    /// GUI der Umgebungs-Picker (bzw. wird die Umgebung implizit aus dem Navigations-Segment bestimmt).
    /// </para>
    /// <para>
    /// Die Verweise sind bewusst <b>Plugin-Namen</b> (Strings), keine Typen: Store und ActivityCatalog einer
    /// Umgebung werden zur Laufzeit ueber die WebPlugin-Schnittstelle namentlich aufgeloest. So bleibt die
    /// Einstellung serialisierbar und der konkrete Kontext-/Aktivitaets-Anbieter Host-Sache.
    /// </para>
    /// </remarks>
    [SettingName("WorkflowEnvironmentSettings")]
    public class WorkflowEnvironmentSettings
    {
        /// <summary>
        /// Die konfigurierten Umgebungen. Leer = bisheriges Ein-Umgebungs-Verhalten (kein Picker, DI-Standard).
        /// </summary>
        public List<WorkflowEnvironment> Environments { get; set; } = new List<WorkflowEnvironment>();
    }

    /// <summary>
    /// Eine Workflow-<b>Umgebung</b>: ein in sich geschlossener Workflow-Datenbestand (ein Store/eine DB)
    /// samt der Angabe, welche Views darauf arbeiten duerfen. Designer und Task-Monitor bedienen immer die
    /// ganze Umgebung.
    /// </summary>
    public class WorkflowEnvironment
    {
        /// <summary>
        /// Technischer, eindeutiger Name der Umgebung. Dient als Schluessel und als (implizites)
        /// Navigations-Segment (z.B. <c>/Workflow/Definitions/{Name}</c>).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>Anzeigename fuer den Picker; faellt auf <see cref="Name"/> zurueck, wenn leer.</summary>
        public string? DisplayName { get; set; }

        /// <summary>Ob in dieser Umgebung der (Web-)Background-Worker laeuft.</summary>
        public bool UseWorker { get; set; }

        /// <summary>Ob der grafische Designer auf dieser Umgebung angeboten wird.</summary>
        public bool UseDesigner { get; set; }

        /// <summary>Ob die Admin-/Monitoring-Views auf dieser Umgebung angeboten werden.</summary>
        public bool UseAdmin { get; set; }

        /// <summary>Ob die Benutzer-Aufgaben-/Formular-Views auf dieser Umgebung angeboten werden.</summary>
        public bool UseForms { get; set; }

        /// <summary>
        /// Name des WebPlugins, das den <c>WorkflowContext</c> (EF) dieser Umgebung liefert. Die
        /// View-Operation umschliesst ihn wie gewohnt mit dem <c>EfWorkflowStore</c> - nur eben pro
        /// Umgebung namentlich aufgeloest statt fest per DI.
        /// </summary>
        public string? WorkflowStorePluginName { get; set; }

        /// <summary>
        /// Die <b>Service-Instanzen</b> (Worker) dieser Umgebung. Je Instanz ein eigener ActivityCatalog:
        /// eine automatisierte Aktivitaet verweist ueber ihr <c>ExecutionTarget</c> auf die Instanz, auf der
        /// sie laufen soll, und der Editor fragt genau deren Catalog nach den zulaessigen Aktivitaeten und
        /// Parametern. Leer = die Umgebung hat (noch) keine deklarierten Worker.
        /// </summary>
        public List<WorkflowEnvironmentInstance> Instances { get; set; } = new List<WorkflowEnvironmentInstance>();
    }

    /// <summary>
    /// Eine <b>Service-Instanz</b> (ein Worker) einer Umgebung: der Ort, an dem automatisierte Aktivitaeten
    /// ausgefuehrt werden. Ihr Name ist zugleich das <c>ExecutionTarget</c>, mit dem eine Aktivitaet auf
    /// diese Instanz verwiesen wird.
    /// </summary>
    public class WorkflowEnvironmentInstance
    {
        /// <summary>
        /// Technischer Name der Instanz. Entspricht dem <c>ExecutionTarget</c> einer automatisierten
        /// Aktivitaet, die auf diesem Worker laufen soll.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>Anzeigename; faellt auf <see cref="Name"/> zurueck, wenn leer.</summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Name des WebPlugins, das den <c>IWorkflowActivityCatalog</c> dieser Instanz liefert - ueber ihn
        /// erfragt der Editor die auf diesem Worker verfuegbaren Aktivitaets-Typen und deren Parameter.
        /// </summary>
        public string? ActivityCatalogPluginName { get; set; }

        /// <summary>
        /// Weitere instanz-spezifische Service-Plugin-Namen (frei, Schluessel = logische Rolle). Offen fuer
        /// kuenftige, instanzgebundene Dienste, ohne die Einstellung erneut aendern zu muessen.
        /// </summary>
        public Dictionary<string, string> Services { get; set; } = new Dictionary<string, string>();
    }
}
