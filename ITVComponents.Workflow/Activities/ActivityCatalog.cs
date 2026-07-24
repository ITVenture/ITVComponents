using System.Collections.Generic;

namespace ITVComponents.Workflow.Activities
{
    /// <summary>Die Art, wie ein Aktivitaets-Parameter im Editor bearbeitet wird.</summary>
    public enum ActivityParameterKind
    {
        /// <summary>Einzeiliger Text.</summary>
        String,

        /// <summary>Mehrzeiliger Text (z.B. ein Script-Rumpf).</summary>
        Multiline,

        /// <summary>Zahl (ganz oder dezimal).</summary>
        Number,

        /// <summary>Ja/Nein-Flag.</summary>
        Bool,

        /// <summary>Auswahl aus einer Liste zulaessiger Werte (statisch oder ueber einen Provider).</summary>
        Picklist,

        /// <summary>Maskierte Eingabe (z.B. Passwort).</summary>
        Password,

        /// <summary>CScript-Ausdruck (zur Laufzeit gegen die Variablen ausgewertet).</summary>
        Expression
    }

    /// <summary>Die Richtung eines Aktivitaets-Parameters (Datenfluss).</summary>
    public enum ActivityParameterDirection
    {
        /// <summary>Eingabewert der Aktivitaet.</summary>
        Input,

        /// <summary>Ergebnis der Aktivitaet.</summary>
        Output
    }

    /// <summary>Ein zulaessiger Wert eines Auswahl-Parameters (Wert + Anzeigelabel).</summary>
    public sealed class ActivityParameterValue
    {
        /// <summary>Der Wert, der in der Konfiguration gespeichert wird.</summary>
        public object Value { get; set; }

        /// <summary>Der Anzeigetext (faellt auf den Wert zurueck, wenn leer).</summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// Die (nicht instanzierte) Beschreibung eines Aktivitaets-Parameters - das standardisierte Model,
    /// das der <see cref="IWorkflowActivityCatalog"/> aus den Klassen-Attributen einer Aktivitaet
    /// erzeugt und dem Editor liefert. Bewusst serialisierbar (keine <c>System.Type</c>-Objekte), damit
    /// es auch ueber Prozessgrenzen (spaeter IPC) getragen werden kann.
    /// </summary>
    public sealed class ActivityParameter
    {
        /// <summary>Der Parametername (Schluessel im Konfigurations-Dictionary).</summary>
        public string Name { get; set; } = "";

        /// <summary>Die Editor-Art.</summary>
        public ActivityParameterKind Kind { get; set; }

        /// <summary>Die Datenfluss-Richtung.</summary>
        public ActivityParameterDirection Direction { get; set; }

        /// <summary>Pflichtfeld?</summary>
        public bool Required { get; set; }

        /// <summary>Vorgabewert, oder null.</summary>
        public object Default { get; set; }

        /// <summary>Beschreibung/Hilfetext, oder null.</summary>
        public string Description { get; set; }

        /// <summary>Optionale Gruppierung im Formular.</summary>
        public string Group { get; set; }

        /// <summary>Sortierung im Formular.</summary>
        public int Order { get; set; }

        /// <summary>Statische Auswahlwerte (bei <see cref="ActivityParameterKind.Picklist"/>), oder leer.</summary>
        public IReadOnlyList<ActivityParameterValue> Values { get; set; } = new List<ActivityParameterValue>();

        /// <summary>
        /// True, wenn die Auswahlwerte dynamisch ueber einen Provider ermittelt werden - der Editor holt
        /// sie dann bei Bedarf ueber <see cref="IWorkflowActivityCatalog.GetValidValues"/>.
        /// </summary>
        public bool HasDynamicValues { get; set; }
    }

    /// <summary>Die Kurzbeschreibung eines verfuegbaren Aktivitaets-Typs.</summary>
    public sealed class ActivityTypeInfo
    {
        /// <summary>Der logische Verweis (ActivityRef), der am Knoten hinterlegt wird.</summary>
        public string ActivityRef { get; set; } = "";

        /// <summary>Anzeigename.</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Beschreibung, oder null.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Fragt ab, welche Aktivitaets-Typen in der Zielumgebung verfuegbar sind und welche Parameter eine
    /// bestimmte Aktivitaet kennt - ohne die Aktivitaet zu instanzieren.
    /// </summary>
    /// <remarks>
    /// Eine In-Process-Implementierung reflektiert die registrierten Aktivitaets-Plugins; eine spaetere
    /// IPC-Variante reicht dieselben Fragen an einen Backend-Dienst weiter, in dem die Plugins leben.
    /// Der Editor haengt nur an diesem Vertrag.
    /// </remarks>
    public interface IWorkflowActivityCatalog
    {
        /// <summary>Liefert die verfuegbaren Aktivitaets-Typen.</summary>
        IReadOnlyList<ActivityTypeInfo> GetActivityTypes();

        /// <summary>Liefert die deklarierten Parameter einer Aktivitaet, oder leer.</summary>
        IReadOnlyList<ActivityParameter> GetParameters(string activityRef);

        /// <summary>
        /// Ermittelt die zulaessigen Werte eines Auswahl-Parameters - fuer dynamische Provider. Laeuft in
        /// einem eigenen, danach abgeraeumten Scope. Liefert die statischen Werte, falls kein Provider
        /// hinterlegt ist.
        /// </summary>
        IReadOnlyList<ActivityParameterValue> GetValidValues(string activityRef, string parameterName);
    }
}
