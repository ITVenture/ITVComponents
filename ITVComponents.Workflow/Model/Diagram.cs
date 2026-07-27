using System.Collections.Generic;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Rein grafische Angaben zu einem Knoten. Die Engine wertet sie nicht aus; sie sind fuer den
    /// spaeteren visuellen Modeler da und werden mit der Definition serialisiert.
    /// </summary>
    public class DiagramShape
    {
        /// <summary>Position der oberen linken Ecke auf der Modellierflaeche.</summary>
        public double X { get; set; }

        /// <summary>Position der oberen linken Ecke auf der Modellierflaeche.</summary>
        public double Y { get; set; }

        /// <summary>Breite des Knotens, oder null fuer die Standardgroesse des Modelers.</summary>
        public double? Width { get; set; }

        /// <summary>Hoehe des Knotens, oder null fuer die Standardgroesse des Modelers.</summary>
        public double? Height { get; set; }
    }

    /// <summary>
    /// Ein Stuetzpunkt einer Kante. Wie <see cref="DiagramShape"/> rein grafisch.
    /// </summary>
    public class DiagramPoint
    {
        /// <summary>Initialisiert einen leeren Stuetzpunkt.</summary>
        public DiagramPoint()
        {
        }

        /// <summary>Initialisiert einen Stuetzpunkt mit Koordinaten.</summary>
        public DiagramPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>X-Koordinate des Stuetzpunkts.</summary>
        public double X { get; set; }

        /// <summary>Y-Koordinate des Stuetzpunkts.</summary>
        public double Y { get; set; }
    }
}
