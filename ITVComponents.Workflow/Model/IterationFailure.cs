using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ITVComponents.Json.Contracts;

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
    /// Der Typ traegt seine Felder <b>einzeln</b> in die Ablage (<see cref="IManualSerializer"/>). Der
    /// Grund ist <see cref="Item"/>: ein <c>object</c>-Feld <i>innerhalb</i> einer Nutzlast bekommt keine
    /// Typkennung und kaeme sonst als roher JSON-Knoten zurueck statt als das, was hineingelegt wurde.
    /// Angemeldet wird der Typ von <c>WorkflowJson</c>; nicht <c>sealed</c>, weil der Contract-Resolver
    /// Polymorphie-Optionen daran haengt.
    /// </para></remarks>
    public class IterationFailure : IManualSerializer
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

        /// <summary>Die Ablage-Form (siehe Klassen-Anmerkung). Kein Feld fuer den taeglichen Gebrauch.</summary>
        public IList<ManualSerializationData> Data { get; set; }

        /// <inheritdoc/>
        public void GetObjectData()
        {
            Data.Add(ManualSerializationData.FromValue(nameof(Item), Item));
            Data.Add(ManualSerializationData.FromValue(nameof(Index), Index));
            Data.Add(ManualSerializationData.FromValue(nameof(Message), Message));
            Data.Add(ManualSerializationData.FromValue(nameof(ExceptionType), ExceptionType));
            Data.Add(ManualSerializationData.FromValue(nameof(ExceptionDetail), ExceptionDetail));
        }

        /// <inheritdoc/>
        public void ApplyObjectData()
        {
            Item = Value(nameof(Item));
            Index = Value(nameof(Index)) is int index ? index : 0;
            Message = Value(nameof(Message)) as string;
            ExceptionType = Value(nameof(ExceptionType)) as string;
            ExceptionDetail = Value(nameof(ExceptionDetail)) as string;
        }

        private object Value(string name) => Data.FirstOrDefault(n => n.PropertyName == name)?.Data;

        /// <inheritdoc/>
        public override string ToString()
        {
            return WasThrown
                ? $"[{Index}] {ExceptionType}: {Message}"
                : $"[{Index}] {Message}";
        }
    }
}
