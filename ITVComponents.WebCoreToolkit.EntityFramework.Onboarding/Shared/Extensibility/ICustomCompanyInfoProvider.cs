using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Extensibility
{
    /// <summary>
    /// Der Zugang zu den konfigurierten Zusatzangaben-Modulen. Er laedt sie, fragt sie und gibt sie
    /// wieder frei - die Oberflaeche bekommt nur Daten zu sehen und haelt selbst nie ein Modul.
    /// </summary>
    /// <remarks>
    /// Jede Methode oeffnet die Module fuer die Dauer EINES Vorgangs und schliesst sie danach wieder. Ein
    /// Formular steht Minuten offen; wuerde es die Module (und damit deren Lade-Scopes samt allem, was
    /// daran haengt) so lange festhalten, waere das derselbe Fehler wie ein am Circuit haengender
    /// Seiten-Handler.
    /// </remarks>
    public interface ICustomCompanyInfoProvider
    {
        /// <summary>
        /// Beschreibt die Reiter, die in diesem Fall zu zeigen sind - in der konfigurierten Reihenfolge.
        /// Leer, wenn nichts konfiguriert ist oder sich kein Modul zustaendig fuehlt.
        /// </summary>
        /// <param name="ctx">der Erfassungsfall</param>
        /// <param name="loadExisting">
        /// ob die bereits abgelegten Angaben zur Vorbelegung geladen werden sollen (Nachtrag im
        /// Firmenprofil). Bei der Anlage sinnlos - dort gibt es den Tenant noch nicht.
        /// </param>
        /// <param name="ct">Abbruch</param>
        Task<IReadOnlyList<CustomInfoTab>> DescribeAsync(CustomInfoContext ctx, bool loadExisting = false,
            CancellationToken ct = default);

        /// <summary>
        /// Laesst die Module ihre Angaben fachlich pruefen. Haelt beim ersten beanstandenden Modul an -
        /// der Benutzer soll eine Beanstandung nach der anderen beheben, nicht eine Liste vorgelegt
        /// bekommen, deren spaetere Eintraege von der ersten abhaengen koennen.
        /// </summary>
        Task<CustomInfoCheckResult> ValidateAsync(CustomInfoContext ctx,
            IReadOnlyDictionary<string, JsonNode> buckets, CancellationToken ct = default);

        /// <summary>
        /// Laesst die Module ihre Angaben ablegen. Wird erst gerufen, wenn der Tenant besteht.
        /// </summary>
        /// <remarks>
        /// Ein Modul, das dabei scheitert, haelt die uebrigen nicht auf: der Tenant existiert bereits, und
        /// ein Abbruch in der Mitte wuerde nur noch mehr Angaben liegen lassen. Was gescheitert ist und
        /// was gar nicht erst mitgekommen ist, steht im Ergebnis - und im Log.
        /// </remarks>
        Task<CustomInfoPersistResult> PersistAsync(CustomInfoContext ctx,
            IReadOnlyDictionary<string, JsonNode> buckets, CancellationToken ct = default);
    }

    /// <summary>
    /// Ein Reiter der Zusatzangaben - alles, was die Oberflaeche zum Zeichnen braucht, ohne das Modul
    /// selbst zu kennen.
    /// </summary>
    public class CustomInfoTab
    {
        /// <summary>Der Schluessel des Moduls - zugleich der Name seines Datensatzes.</summary>
        public string Key { get; set; }

        /// <summary>Die Beschriftung, UNUEBERSETZT (Klartext oder Kultur-JSON).</summary>
        public string Title { get; set; }

        /// <summary>Optionales Symbol, oder leer.</summary>
        public string Icon { get; set; }

        /// <summary>
        /// Der Schluessel einer eigenen Maske, oder leer fuer die generische aus <see cref="Fields"/>.
        /// </summary>
        public string ViewKey { get; set; }

        /// <summary>Die Berechtigung fuers Bearbeiten, oder leer fuer die des Firmenprofils.</summary>
        public string EditPermission { get; set; }

        /// <summary>Die Felder der generischen Maske. Leer, wenn eine eigene Maske gezeichnet wird.</summary>
        public IReadOnlyList<CustomInfoField> Fields { get; set; }

        /// <summary>
        /// Die bereits abgelegten Angaben zur Vorbelegung, oder null. Nur belegt, wenn beim Beschreiben
        /// danach gefragt wurde.
        /// </summary>
        public JsonNode Existing { get; set; }
    }

    /// <summary>Das Ergebnis der fachlichen Pruefung ueber alle Module.</summary>
    public class CustomInfoCheckResult
    {
        private CustomInfoCheckResult(bool valid, string handlerKey, string message, string fieldName)
        {
            Valid = valid;
            HandlerKey = handlerKey;
            Message = message;
            FieldName = fieldName;
        }

        /// <summary>Haben alle Module zugestimmt?</summary>
        public bool Valid { get; }

        /// <summary>Das beanstandende Modul, sonst leer.</summary>
        public string HandlerKey { get; }

        /// <summary>Der anzeigbare Grund (Klartext oder Kultur-JSON), sonst leer.</summary>
        public string Message { get; }

        /// <summary>Das betroffene Feld, sofern benannt.</summary>
        public string FieldName { get; }

        /// <summary>Alle zufrieden.</summary>
        public static CustomInfoCheckResult Ok() => new CustomInfoCheckResult(true, null, null, null);

        /// <summary>Ein Modul hat beanstandet.</summary>
        public static CustomInfoCheckResult Rejected(string handlerKey, string message, string fieldName)
            => new CustomInfoCheckResult(false, handlerKey, message, fieldName);
    }

    /// <summary>Das Ergebnis des Ablegens ueber alle Module.</summary>
    public class CustomInfoPersistResult
    {
        /// <summary>Die Module, deren Ablegen fehlgeschlagen ist.</summary>
        public IReadOnlyList<string> Failed { get; set; } = new string[0];

        /// <summary>
        /// Die Module, die zustaendig waeren, zu denen aber gar keine Angaben vorlagen. Das passiert, wenn
        /// zwischen dem Erfassen und dem Fertigstellen eines Vorgangs ein Modul hinzukam - der Benutzer
        /// kann sie nur noch im Firmenprofil nachtragen.
        /// </summary>
        public IReadOnlyList<string> Missing { get; set; } = new string[0];

        /// <summary>Ist alles abgelegt, was abzulegen war?</summary>
        public bool Complete => Failed.Count == 0 && Missing.Count == 0;
    }
}
