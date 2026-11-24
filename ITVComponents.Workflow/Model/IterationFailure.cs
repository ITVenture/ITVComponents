using System.Text.Json.Serialization;

namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Ein gescheitertes Element einer <see cref="ActivityIteration"/> - Element und Ursache an EINEM
    /// Ort. Das ist der Inhalt von <see cref="ActivityIteration.FailedItemsOutput"/>.
    /// </summary>
    /// <remarks>
    /// Diese Liste ist die <b>Diagnose</b>-Sicht. Den Wiederanlauf regelt
    /// <see cref="ActivityIteration.PendingItemsOutput"/> - der die blanken Original-Elemente fuehrt, weil
    /// die wieder in die Sammlung passen muessen. Genau diese Arbeitsteilung erlaubt es, hier eine
    /// reichere Form zu fuehren, ohne den Retry zu verkomplizieren.
    /// <para>
    /// <b>Die Ausnahme steht als Daten drin, nicht als Objekt.</b> Ein <c>Exception</c>-Objekt in einer
    /// Workflow-Variable waere ein Fehler mit Ansage: die Variablen werden beim Commit als JSON abgelegt,
    /// und <c>System.Text.Json</c> stolpert dabei ueber <c>Exception.TargetSite</c>
    /// (<c>MethodBase</c> ist nicht serialisierbar) - der Commit wuerde ausgerechnet dann scheitern, wenn
    /// die Arbeit schon getan ist. <see cref="ExceptionType"/> und <see cref="ExceptionDetail"/> tragen
    /// dasselbe, was man zur Diagnose braucht (Typ und Stacktrace), und ueberleben die Persistenz.
    /// </para>
    /// <para>
    /// <see cref="Item"/> ist ein <c>object</c>-Feld und traegt in der Ablage seine eigene Typkennung
    /// (<c>SerializationTypingMode.EnvelopePolymorphism</c>) - es kommt als das zurueck, was hineingelegt
    /// wurde. Der Typ selbst ist von <c>WorkflowJson</c> unter einem Kurznamen angemeldet.
    /// </para></remarks>
    public class IterationFailure
    {
        /// <summary>Das unveraenderte Element aus der Eingabe-Sammlung.</summary>
        public object Item { get; set; }

        /// <summary>Seine 0-basierte Position in der Eingabe-Sammlung.</summary>
        public int Index { get; set; }

        /// <summary>
        /// Die Fehlermeldung - die von <c>WorkflowActivityContext.Fail</c> uebergebene oder, bei einer
        /// Ausnahme, deren <c>Message</c>. Immer gesetzt.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Der vollstaendige Typname der geworfenen Ausnahme, oder <c>null</c>, wenn die Aktivitaet den
        /// Fehler <b>kontrolliert</b> ueber <c>Fail</c> gemeldet hat. Der Unterschied ist fuer die
        /// Diagnose der wichtigste: „hat abgelehnt" ist etwas anderes als „ist abgestuerzt".
        /// </summary>
        public string ExceptionType { get; set; }

        /// <summary>
        /// Die ausgeschriebene Ausnahme inkl. Stacktrace (und inneren Ausnahmen), oder <c>null</c> bei
        /// einem kontrollierten Fehler.
        /// </summary>
        public string ExceptionDetail { get; set; }

        /// <summary>Wurde eine Ausnahme geworfen (statt <c>Fail</c> gerufen)?</summary>
        [JsonIgnore]
        public bool WasThrown => ExceptionType != null;

        /// <inheritdoc/>
        public override string ToString()
        {
            return WasThrown
                ? $"[{Index}] {ExceptionType}: {Message}"
                : $"[{Index}] {Message}";
        }
    }
}
