using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Caching
{
    /// <summary>
    /// How long changes of a topic are collected before <see cref="IEntityChangeSignal.Changed"/> is raised
    /// once for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Einstellung gilt <b>prozessweit je Thema</b> und nicht je Kontext: Themennamen sind ohnehin
    /// prozessweit eindeutig, und die datenbank-getriebene Fassung kaeme gar nicht an einen Kontext
    /// gebunden an. Ein Thema, das es in mehreren Kontexten gibt, bekommt damit ueberall dasselbe Fenster -
    /// was gemeint ist, denn es beschreibt die Eile der Sache, nicht die Datenquelle.
    /// </para>
    /// <para>
    /// Der Sinn: eine Meldung gilt einer Menge von TABELLEN, nicht einer Zeile. Ein Schreiber, der einen
    /// Schwung Zeilen bewegt (ein Runner, der mehrere Instanzen vortreibt, ein Import), erzeugt damit einen
    /// Schwung Meldungen - und jeder Empfaenger, der daraufhin nachsieht, tut das genauso oft. Ein
    /// Sammelfenster macht daraus EINE Meldung.
    /// </para>
    /// <para>
    /// <b>Nur der Weckruf wird gesammelt, nie der Zeitstempel.</b> <c>GetLastChange</c> (und der
    /// Schreib-Verfolger darunter) bleibt synchron - die puffernden Verbraucher, die beim naechsten Zugriff
    /// selbst nachsehen (Navigation, Berechtigungs-Bereich, Fremdschluessel-Beschriftungen), sehen eine
    /// Aenderung also weiterhin sofort. Verzoegert wird ausschliesslich das aktive Benachrichtigen.
    /// </para>
    /// <para>
    /// Das Fenster beginnt mit der ERSTEN Meldung und feuert an seinem Ende einmal. Es ist damit eine
    /// Obergrenze fuer die Verzoegerung - kein Verhungern bei Dauerlast, wie es ein Fenster haette, das
    /// jede neue Meldung neu startet.
    /// </para>
    /// </remarks>
    /// <example>
    /// Als Einstellung des Hosts (WebPart-Sektion oder <c>services.Configure</c>) oder als
    /// GlobalSettings-Eintrag <c>EntityChangeSignal</c>:
    /// <code>
    /// { "DefaultMilliseconds": 0, "Topics": { "WorkflowProgress": 250 } }
    /// </code>
    /// </example>
    public class EntitySignalDebounceSettings
    {
        /// <summary>
        /// Das Sammelfenster in Millisekunden fuer Themen ohne eigene Angabe. 0 (Vorgabe) = sofort melden.
        /// </summary>
        public int DefaultMilliseconds { get; set; }

        /// <summary>
        /// Das Sammelfenster je Thema, in Millisekunden. Was hier steht, gewinnt gegen
        /// <see cref="DefaultMilliseconds"/>.
        /// </summary>
        /// <remarks>
        /// Themen sind unterschiedlich eilig: eine Rechte-Aenderung soll sofort ziehen (0), waehrend die
        /// Fortschritts-Meldung eines Workflows ein Viertel einer Sekunde warten darf, ohne dass es jemand
        /// bemerkt. Ein einziger Wert fuer alles waere deshalb entweder zu hektisch oder zu traege.
        /// </remarks>
        public Dictionary<string, int> Topics { get; set; }
            = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Wie oft die datenbank-getriebene Fassung dieser Einstellungen neu gelesen wird, in Sekunden.
        /// 0 (Vorgabe) = nur einmal, beim ersten Bedarf.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Gedacht fuer Zeiten, in denen an den Fenstern noch gedreht wird: mit einem Zyklus zieht eine
        /// Aenderung am GlobalSettings-Eintrag von selbst, ohne Neustart. Im eingeschwungenen Betrieb
        /// gehoert er auf 0 - jeder Zyklus ist eine Datenbank-Abfrage, die sonst nie noetig waere.
        /// </para>
        /// <para>
        /// <b>Dieser Wert wird nur aus der Host-Einstellung gelesen, nicht aus der Datenbank.</b> Sonst
        /// koennte sich ein Eintrag mit <c>0</c> selbst aussperren: das Nachladen waere aus, und die einzige
        /// Stelle, an der man es wieder einschalten koennte, wuerde nicht mehr gelesen.
        /// </para>
        /// </remarks>
        public int RefreshSeconds { get; set; }

        /// <summary>
        /// Liefert das Sammelfenster fuer ein Thema.
        /// </summary>
        /// <param name="topic">das Thema</param>
        /// <returns>die Dauer, oder <see cref="TimeSpan.Zero"/> fuer "sofort"</returns>
        public TimeSpan WindowFor(string topic)
        {
            int ms = DefaultMilliseconds;
            if (!string.IsNullOrEmpty(topic) && Topics != null && Topics.TryGetValue(topic, out int own))
            {
                ms = own;
            }

            return ms > 0 ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;
        }

        /// <summary>Der Name des GlobalSettings-Eintrags, aus dem diese Einstellungen gelesen werden.</summary>
        /// <remarks>
        /// Der Weg fuer Hosts, die ihre Kontexte ueber das Plugin-System bauen: dort gibt es keine
        /// <c>Services.Configure</c>-Gelegenheit mehr, wohl aber die datenbank-getriebenen Einstellungen.
        /// </remarks>
        public const string GlobalSettingName = "EntityChangeSignal";
    }
}
