using System.Collections.Generic;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Eine gerichtete Kante zwischen zwei Knoten. Traegt optional eine CScript-Bedingung, mit der
    /// ein exklusives Gateway (XOR) den Ausgang waehlt.
    /// </summary>
    public class SequenceFlow
    {
        /// <summary>Innerhalb der Definition eindeutige Kennung der Kante.</summary>
        public string Id { get; set; }

        /// <summary>Id des Quellknotens.</summary>
        public string SourceId { get; set; }

        /// <summary>Id des Zielknotens.</summary>
        public string TargetId { get; set; }

        /// <summary>Optionaler Anzeigename (Kantenbeschriftung im Modeler).</summary>
        public string Name { get; set; }

        /// <summary>
        /// Optionaler CScript-Ausdruck, der ueber den Variablen der Instanz einen booleschen Wert
        /// liefert. Nur an ausgehenden Kanten eines exklusiven Gateways ausgewertet.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Optionales Mapping der Kante: wie der Variablen-Stack aussieht, wenn ein Token <b>hier
        /// ankommt</b>. <see cref="ActivityInputBinding.Parameter"/> ist der Name der zu setzenden
        /// Instanz-Variable. Leer = die Kante reicht den Stack unveraendert weiter (bisheriges Verhalten).
        /// </summary>
        /// <remarks>
        /// Ergaenzt - nicht ersetzt - das Mapping am Knoten: die <b>Kante</b> normalisiert den Stack (typisch,
        /// wenn mehrere Pfade auf denselben Knoten laufen und ihn mit unterschiedlichen Variablennamen
        /// erreichen), die <b>Aktivitaet</b> zieht daraus ihre Parameter. Reihenfolge an einem XOR: erst
        /// waehlt die <see cref="Condition"/> die Kante, dann greift deren Mapping.
        /// </remarks>
        public List<ActivityInputBinding> Inputs { get; set; } = new List<ActivityInputBinding>();

        /// <summary>
        /// Wie das Mapping der Kante in den Scope einfliesst. Standard
        /// <see cref="ActivityScopeMode.Extend"/> (additiv). <see cref="ActivityScopeMode.Replace"/>
        /// konsolidiert: nach der Kante besteht der Stack genau aus <see cref="Inputs"/> plus
        /// <see cref="RetainVariables"/>. Nur auf einem Ein-Zweig-Segment sinnvoll - der Validator warnt
        /// innerhalb einer parallelen Region.
        /// </summary>
        public ActivityScopeMode ScopeMode { get; set; } = ActivityScopeMode.Extend;

        /// <summary>
        /// Bei <see cref="ActivityScopeMode.Replace"/>: Variablen, die trotz Konsolidierung erhalten
        /// bleiben. Bei <see cref="ActivityScopeMode.Extend"/> ohne Wirkung.
        /// </summary>
        public List<string> RetainVariables { get; set; } = new List<string>();

        /// <summary>Grafische Stuetzpunkte fuer den Modeler; von der Engine ignoriert.</summary>
        public List<DiagramPoint> Waypoints { get; set; } = new List<DiagramPoint>();
    }
}
