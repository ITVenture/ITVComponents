using System;

namespace ITVComponents.Workflow.Instances
{
    /// <summary>
    /// Die Prioritaets-Stufen einer Workflow-Instanz. <b>Kleinere Zahl = wichtiger</b> - dieselbe
    /// Konvention wie bei <c>ITVComponents.ParallelProcessing.ITask.Priority</c>, damit der Wert
    /// unveraendert an die Ausfuehrungsschicht durchgereicht werden kann.
    /// </summary>
    /// <remarks>
    /// Die Stufen sind absichtlich Konstanten und kein Enum: die Ausfuehrungsschicht rechnet mit
    /// <c>int</c>, und Zwischenwerte (z.B. 2 statt <see cref="Normal"/>) sollen erlaubt bleiben. Der
    /// Wert wirkt nur auf die <b>Reihenfolge</b> der Hintergrund-Abarbeitung: ein hoeher priorisierter
    /// Workflow kommt deutlich oefter dran als ein niedriger - verhungern kann ein niedriger aber nicht
    /// (der Task-Processor gewichtet, er sperrt nicht).
    /// </remarks>
    public static class WorkflowPriority
    {
        /// <summary>Hoechste Stufe (0) - draengt sich vor allem anderen vor.</summary>
        public const int Highest = 0;

        /// <summary>Hoch (1).</summary>
        public const int High = 1;

        /// <summary>Der Standard (2) - was nichts anderes sagt, laeuft hier.</summary>
        public const int Normal = 2;

        /// <summary>Niedrig (3).</summary>
        public const int Low = 3;

        /// <summary>Niedrigste Stufe (4) - Hintergrund-Fleissarbeit, die jeder ueberholen darf.</summary>
        public const int Lowest = 4;

        /// <summary>Die kleinste (= wichtigste) unterstuetzte Stufe.</summary>
        public const int MostImportant = Highest;

        /// <summary>Die groesste (= unwichtigste) unterstuetzte Stufe.</summary>
        public const int LeastImportant = Lowest;

        /// <summary>
        /// Beschneidet einen Wert auf das angegebene Band. Ein Wert ausserhalb des Bandes waere fuer den
        /// Task-Processor ein Fehler (er haelt je Stufe genau eine Warteschlange) - deshalb wird hier
        /// beschnitten statt geworfen: eine Instanz mit unpassender Stufe soll laufen, nicht scheitern.
        /// </summary>
        public static int Clamp(int priority, int mostImportant = MostImportant, int leastImportant = LeastImportant)
        {
            if (leastImportant < mostImportant)
            {
                throw new ArgumentException(
                    $"The priority band is inverted: mostImportant={mostImportant} must not be greater than " +
                    $"leastImportant={leastImportant} (smaller number = more important).", nameof(leastImportant));
            }

            return priority < mostImportant ? mostImportant : priority > leastImportant ? leastImportant : priority;
        }

        /// <summary>Ein sprechender Name fuer eine Stufe (fuer Anzeige und Protokoll).</summary>
        public static string Name(int priority)
        {
            return priority switch
            {
                Highest => "Highest",
                High => "High",
                Normal => "Normal",
                Low => "Low",
                Lowest => "Lowest",
                _ => priority.ToString()
            };
        }
    }
}
