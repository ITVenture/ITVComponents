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

        /// <summary>Grafische Stuetzpunkte fuer den Modeler; von der Engine ignoriert.</summary>
        public List<DiagramPoint> Waypoints { get; set; } = new List<DiagramPoint>();
    }
}
