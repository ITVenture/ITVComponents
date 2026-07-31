using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.WorkflowViews.Monitoring.ViewModels
{
    /// <summary>
    /// Die Art eines korrigierbaren Wertes. Bewusst eine kleine Liste: sie bestimmt nur, wie der
    /// eingegebene Text in einen Wert zurueckuebersetzt wird - alles Weitere ist Sache des Workflows.
    /// </summary>
    public enum WorkflowVariableKind
    {
        /// <summary>Text.</summary>
        Text,

        /// <summary>Zahl (Ganzzahl oder Dezimal).</summary>
        Number,

        /// <summary>Ja/Nein.</summary>
        Boolean,

        /// <summary>Zeitpunkt.</summary>
        DateTime,

        /// <summary>
        /// Ein zusammengesetzter Wert (Objekt/Liste). Wird nur ANGEZEIGT - ihn im Textfeld zu
        /// bearbeiten waere Raten; wer ihn korrigieren muss, tut das an der Quelle.
        /// </summary>
        Complex
    }

    /// <summary>Eine Variable im Scope des fehlgeschlagenen Schritts.</summary>
    public sealed class WorkflowRetryVariable
    {
        /// <summary>Der Name der Variable.</summary>
        public string Name { get; init; } = "";

        /// <summary>Der aktuelle Wert als Text (fuer die Maske aufbereitet).</summary>
        public string Value { get; init; } = "";

        /// <summary>Die erkannte Art - sie bestimmt das Eingabefeld und die Rueckuebersetzung.</summary>
        public WorkflowVariableKind Kind { get; init; } = WorkflowVariableKind.Text;

        /// <summary>Der Typname des aktuellen Wertes (Anzeige), oder null bei einem Null-Wert.</summary>
        public string? TypeName { get; init; }
    }

    /// <summary>
    /// Ein stehen gebliebener Zweig: ein aktives Token samt seinem Scope. Nach einem Split hat jeder
    /// Zweig seinen EIGENEN Scope - deshalb ist die Korrektur je Zweig zu machen und nicht instanzweit.
    /// </summary>
    public sealed class WorkflowRetryBranch
    {
        /// <summary>Das Token - es IST der Zweig; darueber laeuft die Zuordnung der Korrekturen.</summary>
        public string TokenId { get; init; } = "";

        /// <summary>Der Knoten, auf dem der Zweig steht.</summary>
        public string? NodeId { get; init; }

        /// <summary>Der Anzeigename dieses Knotens (aus der Definition), oder die Id.</summary>
        public string? NodeName { get; init; }

        /// <summary>
        /// Ist DIESER Zweig gescheitert? False bedeutet: er kam nur nicht mehr dran, weil der Vortrieb
        /// beim Fehler eines anderen Zweigs abbrach. Auch er laeuft beim Retry weiter.
        /// </summary>
        public bool Faulted { get; init; }

        /// <summary>Die zu diesem Knoten protokollierte Fehlermeldung, oder null.</summary>
        public string? FaultMessage { get; init; }

        /// <summary>Der Zeitpunkt dieses Fehlers (UTC), oder null.</summary>
        public DateTime? FailedUtc { get; init; }

        /// <summary>
        /// Die Variablen im Scope dieses Zweigs - genau die, die er beim naechsten Versuch liest.
        /// </summary>
        public IReadOnlyList<WorkflowRetryVariable> Variables { get; init; } = Array.Empty<WorkflowRetryVariable>();
    }

    /// <summary>
    /// Alles, was die Retry-Maske braucht: warum es scheiterte und welche Zweige stehen geblieben sind -
    /// mit den Daten, die jeder von ihnen sieht.
    /// </summary>
    public sealed class WorkflowRetryInfo
    {
        /// <summary>Die betroffene Instanz.</summary>
        public string InstanceId { get; init; } = "";

        /// <summary>
        /// Die Fehlermeldung der Instanz. Bei mehreren gleichzeitig gescheiterten Zweigen ist das die
        /// des zuletzt gemeldeten - die einzelnen stehen an den <see cref="Branches"/>.
        /// </summary>
        public string? FaultMessage { get; init; }

        /// <summary>
        /// Kann ueberhaupt wieder aufgesetzt werden? False, wenn der Fehler an keinem Schritt haengt
        /// (dann gibt es keinen Punkt, an dem man ansetzen koennte) - <see cref="Reason"/> sagt warum.
        /// </summary>
        public bool CanRetry { get; init; }

        /// <summary>Der Grund, wenn <see cref="CanRetry"/> false ist.</summary>
        public string? Reason { get; init; }

        /// <summary>
        /// Die stehen gebliebenen Zweige - gescheiterte zuerst (zuletzt gemeldeter Fehler vorne).
        /// </summary>
        public IReadOnlyList<WorkflowRetryBranch> Branches { get; init; } = Array.Empty<WorkflowRetryBranch>();

        /// <summary>Die Knoten-Ids der gescheiterten Zweige - fuer die Markierung im Graphen.</summary>
        public IReadOnlyList<string> FaultedNodeIds
            => Branches.Where(b => b.Faulted && b.NodeId != null).Select(b => b.NodeId!).Distinct().ToList();
    }

    /// <summary>Das Ergebnis eines Wiederaufsatzes - mit Grund, wenn er nicht ging.</summary>
    public sealed class WorkflowRetryResult
    {
        /// <summary>Wurde die Instanz wieder aufgenommen?</summary>
        public bool Resumed { get; init; }

        /// <summary>Der Grund des Scheiterns (anzeigbar), sonst null.</summary>
        public string? Error { get; init; }

        /// <summary>Erzeugt ein Erfolgs-Ergebnis.</summary>
        public static WorkflowRetryResult Ok() => new WorkflowRetryResult { Resumed = true };

        /// <summary>Erzeugt ein Fehler-Ergebnis mit anzeigbarem Grund.</summary>
        public static WorkflowRetryResult Failed(string error)
            => new WorkflowRetryResult { Resumed = false, Error = error };
    }

    /// <summary>
    /// Uebersetzt zwischen Anzeige-Text und Wert. Die Rueckuebersetzung ist wichtig, weil
    /// CScript-Bedingungen typ-empfindlich sind: <c>amount &gt; 0</c> braucht eine Zahl, keinen Text.
    /// </summary>
    public static class WorkflowVariableValue
    {
        /// <summary>Erkennt die Art eines vorhandenen Wertes.</summary>
        public static WorkflowVariableKind KindOf(object? value)
            => value switch
            {
                null => WorkflowVariableKind.Text,
                bool => WorkflowVariableKind.Boolean,
                DateTime => WorkflowVariableKind.DateTime,
                sbyte or byte or short or ushort or int or uint or long or ulong
                    or float or double or decimal => WorkflowVariableKind.Number,
                string => WorkflowVariableKind.Text,
                _ => WorkflowVariableKind.Complex
            };

        /// <summary>Stellt einen Wert als Text dar (invariant bei Zahlen/Daten - er wird so auch zurueckgelesen).</summary>
        public static string Display(object? value)
            => value switch
            {
                null => string.Empty,
                bool b => b ? "true" : "false",
                DateTime d => d.ToString("o", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

        /// <summary>
        /// Liest einen eingegebenen Text als Wert der angegebenen Art. Leerer Text ergibt null (die
        /// Variable wird bewusst geleert). Scheitert das Parsen, wird der Text unveraendert genommen -
        /// besser ein Wert, den der Workflow ablehnt, als eine stille Null.
        /// </summary>
        public static object? Parse(string? text, WorkflowVariableKind kind)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string t = text.Trim();
            switch (kind)
            {
                case WorkflowVariableKind.Boolean:
                    return bool.TryParse(t, out bool b) ? b : (object)t;
                case WorkflowVariableKind.Number:
                    if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    {
                        return l;
                    }

                    return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d)
                           || decimal.TryParse(t, NumberStyles.Any, CultureInfo.CurrentCulture, out d)
                        ? d
                        : (object)t;
                case WorkflowVariableKind.DateTime:
                    return DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                        out DateTime dt)
                        ? dt
                        : DateTime.TryParse(t, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt)
                            ? dt
                            : (object)t;
                default:
                    return t;
            }
        }
    }
}
