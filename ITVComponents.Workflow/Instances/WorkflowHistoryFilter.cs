using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ITVComponents.Workflow.Instances
{
    /// <summary>
    /// Entscheidet, welche Protokolleintraege ueberhaupt im Ausfuehrungsprotokoll einer Instanz landen.
    /// Das Gegenstueck zu den Filtern eines <c>ITVComponents.Logging</c>-Log-Ziels, nur fuer das
    /// <b>Ablauf-Log</b> der Instanz.
    /// </summary>
    /// <remarks>
    /// Der Filter greift auf der <b>Schreib</b>-Seite: was er ablehnt, wird gar nicht erst angehaengt und
    /// damit auch nicht persistiert. Das ist der Zweck - ein Workflow mit vielen Knoten erzeugt sonst je
    /// Schritt zwei Verbose-Eintraege (Entered/Completed), die niemand liest und die die
    /// History-Tabelle fuellen.
    /// </remarks>
    public interface IWorkflowHistoryFilter
    {
        /// <summary>
        /// Soll dieser Eintrag protokolliert werden?
        /// </summary>
        /// <param name="event">die Kurzbezeichnung des Ereignisses (z.B. Entered, Waiting, Faulted)</param>
        /// <param name="nodeId">der betroffene Knoten, oder null</param>
        /// <param name="severity">der Schweregrad</param>
        bool ShouldLog(string @event, string nodeId, HistorySeverity severity);
    }

    /// <summary>
    /// Der Standard-Filter: eine Mindest-Stufe plus optionale Ereignis-Muster (Sperr- und Positivliste).
    /// Ohne Konfiguration laesst er alles durch - das bisherige Verhalten.
    /// </summary>
    /// <remarks>
    /// Der typische Griff ist <c>MinSeverity = HistorySeverity.Info</c>: damit verschwinden die
    /// Schritt-fuer-Schritt-Eintraege (Entered/Completed/Mapped/Parameters), die Meilensteine bleiben.
    /// Wer gezielter aussieben will, nimmt <see cref="SuppressedEvents"/>; wer nur ganz wenige
    /// Ereignisse behalten will, <see cref="AllowedEvents"/>.
    /// <para>
    /// <b>Fehler kommen immer durch.</b> Eintraege ab <see cref="AlwaysLogFrom"/> (Standard
    /// <see cref="HistorySeverity.Error"/>) passieren jeden Filter - ein stillgelegtes Ablauf-Log darf
    /// nicht dazu fuehren, dass ein gefaulteter Workflow keine Spur hinterlaesst.
    /// </para></remarks>
    public class WorkflowHistoryFilter : IWorkflowHistoryFilter
    {
        private static readonly ConcurrentDictionary<string, Regex> patternCache =
            new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);

        private static IWorkflowHistoryFilter defaultFilter = new WorkflowHistoryFilter();

        /// <summary>
        /// Der prozessweite Standard-Filter fuer alle Instanzen, denen kein eigener zugewiesen wurde -
        /// der Platz, an dem eine Anwendung ihre Log-Ausfuehrlichkeit einmalig einstellt (analog zur
        /// Konfiguration eines Log-Ziels beim Start). Nie null; ein Setzen auf null stellt den
        /// durchlassenden Standard wieder her.
        /// </summary>
        public static IWorkflowHistoryFilter Default
        {
            get => defaultFilter;
            set => defaultFilter = value ?? new WorkflowHistoryFilter();
        }

        /// <summary>
        /// Die Mindest-Stufe, ab der ein Eintrag protokolliert wird. Standard
        /// <see cref="HistorySeverity.Verbose"/> = alles.
        /// </summary>
        public HistorySeverity MinSeverity { get; set; } = HistorySeverity.Verbose;

        /// <summary>
        /// Ab dieser Stufe passiert ein Eintrag den Filter <b>immer</b>, unabhaengig von
        /// <see cref="MinSeverity"/> und den Ereignis-Mustern. Standard
        /// <see cref="HistorySeverity.Error"/>.
        /// </summary>
        public HistorySeverity AlwaysLogFrom { get; set; } = HistorySeverity.Error;

        /// <summary>
        /// Ereignis-Namen, die verworfen werden (Sperrliste). Ein <c>*</c> steht fuer beliebig viele
        /// Zeichen, z.B. <c>Boundary*</c>. Gross-/Kleinschreibung spielt keine Rolle. Leer = nichts
        /// gesperrt.
        /// </summary>
        public List<string> SuppressedEvents { get; set; } = new List<string>();

        /// <summary>
        /// Ist die Liste nicht leer, werden <b>nur</b> Ereignisse mit passendem Namen protokolliert
        /// (Positivliste, gleiche Muster-Syntax wie <see cref="SuppressedEvents"/>). Leer = keine
        /// Einschraenkung.
        /// </summary>
        public List<string> AllowedEvents { get; set; } = new List<string>();

        /// <inheritdoc/>
        public virtual bool ShouldLog(string @event, string nodeId, HistorySeverity severity)
        {
            if (severity >= AlwaysLogFrom)
            {
                return true;
            }

            if (severity < MinSeverity)
            {
                return false;
            }

            if (Matches(SuppressedEvents, @event))
            {
                return false;
            }

            return AllowedEvents == null || AllowedEvents.Count == 0 || Matches(AllowedEvents, @event);
        }

        /// <summary>
        /// Liefert diesen Filter mit einer anderen Mindest-Stufe - fuer eine Definition, die
        /// ausfuehrlicher (oder knapper) protokollieren soll als der Rest. Der Aufrufer bekommt eine
        /// Kopie; dieser Filter bleibt unveraendert.
        /// </summary>
        public WorkflowHistoryFilter WithMinSeverity(HistorySeverity minSeverity)
        {
            return new WorkflowHistoryFilter
            {
                MinSeverity = minSeverity,
                AlwaysLogFrom = AlwaysLogFrom,
                SuppressedEvents = new List<string>(SuppressedEvents ?? new List<string>()),
                AllowedEvents = new List<string>(AllowedEvents ?? new List<string>())
            };
        }

        /// <summary>
        /// Legt um einen beliebigen Filter eine abweichende Mindest-Stufe. Fuer einen
        /// <see cref="WorkflowHistoryFilter"/> ist das <see cref="WithMinSeverity"/>, fuer eine fremde
        /// Implementierung ein Vorschalt-Filter (erst die Stufe, dann der innere Filter).
        /// </summary>
        public static IWorkflowHistoryFilter OverrideMinSeverity(IWorkflowHistoryFilter inner,
            HistorySeverity minSeverity)
        {
            if (inner is WorkflowHistoryFilter standard)
            {
                return standard.WithMinSeverity(minSeverity);
            }

            return new MinSeverityGate(inner, minSeverity);
        }

        private static bool Matches(List<string> patterns, string value)
        {
            if (patterns == null || patterns.Count == 0)
            {
                return false;
            }

            value ??= string.Empty;
            foreach (string pattern in patterns)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    continue;
                }

                if (pattern.IndexOf('*') < 0)
                {
                    if (string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    continue;
                }

                if (ToRegex(pattern).IsMatch(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static Regex ToRegex(string pattern)
        {
            return patternCache.GetOrAdd(pattern, p => new Regex(
                "^" + Regex.Escape(p).Replace("\\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));
        }

        /// <summary>Vorschalt-Stufe fuer einen fremden Filter (siehe <see cref="OverrideMinSeverity"/>).</summary>
        private sealed class MinSeverityGate : IWorkflowHistoryFilter
        {
            private readonly IWorkflowHistoryFilter inner;
            private readonly HistorySeverity minSeverity;

            public MinSeverityGate(IWorkflowHistoryFilter inner, HistorySeverity minSeverity)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                this.minSeverity = minSeverity;
            }

            public bool ShouldLog(string @event, string nodeId, HistorySeverity severity)
            {
                if (severity >= HistorySeverity.Error)
                {
                    return true;
                }

                return severity >= minSeverity && inner.ShouldLog(@event, nodeId, severity);
            }
        }
    }
}
