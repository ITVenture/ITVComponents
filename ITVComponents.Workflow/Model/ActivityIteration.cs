namespace ITVComponents.Workflow.Model
{
    /// <summary>
    /// Macht aus EINEM Aktivitaets-Knoten eine <b>Serie ueber eine Sammlung</b>: die Aktivitaet laeuft
    /// einmal je Element, wahlweise mehrere Elemente gleichzeitig. Der Zweig bleibt dabei ein einziger
    /// Zweig - es entstehen keine Tokens, keine Zweig-Sperren und keine zusaetzlichen Commits je
    /// Element.
    /// </summary>
    /// <remarks>
    /// Der Anwendungsfall ist der lange Schritt in einem sonst seriellen Ablauf: 1000 Dateien
    /// signieren, 5000 Datensaetze pruefen. Ein paralleles Gateway waere dafuer das falsche Werkzeug -
    /// es modelliert eine feste Zahl fachlich verschiedener Straenge, nicht n gleiche Elemente, und
    /// jedes davon kostete eine Token-Zeile und einen Commit.
    /// <para>
    /// <b>Die Aktivitaet muss thread-sicher sein</b>, sobald <see cref="MaxParallel"/> groesser als 1
    /// ist: die Engine loest sie EINMAL auf und ruft dieselbe Instanz aus mehreren Threads auf (ein
    /// Plugin wird ueblicherweise unter seinem Namen geteilt - es je Element neu zu laden waere bei 1000
    /// Elementen keine Option). Zustand gehoert deshalb in lokale Variablen, nicht in Felder.
    /// </para>
    /// <para>
    /// Aus demselben Grund arbeitet jeder Element-Lauf auf einer <b>Kopie</b> des Variablen-Scopes:
    /// Schreibzugriffe auf <c>context.Variables</c> wirken nur innerhalb des Element-Laufs und werden
    /// danach verworfen (die Engine protokolliert die betroffenen Namen als Warnung, damit das nicht
    /// still passiert). Ergebnisse gehoeren in <c>context.Outputs</c> - die sammelt die Engine ein.
    /// </para></remarks>
    public class ActivityIteration
    {
        /// <summary>
        /// Der Name des <b>Eingabeparameters</b>, dessen aufgeloester Wert die Sammlung ist. Muss auf
        /// eine Eingabe-Bindung des Knotens zeigen und zur Laufzeit etwas Aufzaehlbares liefern (null
        /// gilt als leere Sammlung; eine Zeichenkette wird bewusst NICHT in Zeichen zerlegt).
        /// </summary>
        public string ItemsInput { get; set; }

        /// <summary>
        /// Unter welchem Eingabeparameter der Element-Lauf sein Element bekommt. Leer = unter
        /// <see cref="ItemsInput"/> (die Aktivitaet sieht dann statt der Sammlung genau ein Element).
        /// Wird hier ein anderer Name gesetzt, bleibt die vollstaendige Sammlung zusaetzlich unter
        /// <see cref="ItemsInput"/> sichtbar.
        /// </summary>
        public string ItemParameter { get; set; }

        /// <summary>
        /// Optional: unter welchem Eingabeparameter der Element-Lauf seinen 0-basierten Index bekommt.
        /// Leer = kein Index.
        /// </summary>
        public string IndexParameter { get; set; }

        /// <summary>
        /// Wie viele Elemente gleichzeitig laufen duerfen. 1 (Standard) = streng nacheinander, also
        /// dasselbe Verhalten wie eine Aktivitaet, die selbst durch die Sammlung laeuft. 0 oder kleiner =
        /// so viele wie Prozessorkerne. Parallelitaet ist damit ein bewusster Griff, kein Nebeneffekt.
        /// </summary>
        public int MaxParallel { get; set; } = 1;

        /// <summary>
        /// Was bei einem fehlgeschlagenen Element passiert. False (Standard) = <b>abbrechen</b>: bereits
        /// laufende Elemente laufen aus, keine neuen werden begonnen, und der Knoten scheitert wie eine
        /// gewoehnliche Aktivitaet (Fehler-Ausgang, sonst faultet die Instanz). True = <b>durchlaufen</b>:
        /// alle Elemente werden versucht; scheitert mindestens eines, scheitert der Knoten am Ende - dann
        /// aber mit vollstaendigem Ergebnis (siehe <see cref="FailedItemsOutput"/>).
        /// </summary>
        public bool ContinueOnError { get; set; }

        /// <summary>
        /// Optional: der Ausgabeparameter, der die <b>Fehler</b> aufnimmt - je gescheitertem Element ein
        /// <see cref="IterationFailure"/> mit Element, Position und Ursache, in Eingabe-Reihenfolge.
        /// Leer = nicht sammeln.
        /// </summary>
        /// <remarks>
        /// Das ist die <b>Diagnose</b>-Sicht: Element und Ursache zusammen, statt zweier Listen, die man
        /// ueber den Index in Deckung bringen muss. Fuer den <b>Wiederanlauf</b> ist
        /// <see cref="PendingItemsOutput"/> gedacht - der fuehrt die blanken Original-Elemente.
        /// </remarks>
        public string FailedItemsOutput { get; set; }

        /// <summary>
        /// Optional: der Ausgabeparameter, der die Anzahl der <b>erfolgreichen</b> Elemente aufnimmt.
        /// Leer = nicht setzen.
        /// </summary>
        /// <remarks>
        /// Zaehlt nur DIESEN Durchlauf, ohne <see cref="CarryOverInput"/> - sonst waere es beim zweiten
        /// Versuch nicht mehr die Zahl der hier erledigten Elemente.
        /// </remarks>
        public string SucceededCountOutput { get; set; }

        /// <summary>
        /// Optional: der Ausgabeparameter, der <b>alles noch Offene</b> aufnimmt - die gescheiterten
        /// <b>und</b> die nach einem Abbruch nie versuchten Elemente, in Eingabe-Form und
        /// Eingabe-Reihenfolge. <b>Das ist der richtige Eingang fuer einen Wiederholungs-Durchlauf.</b>
        /// Leer = nicht sammeln.
        /// </summary>
        /// <remarks>
        /// Der Unterschied zu <see cref="FailedItemsOutput"/> zeigt sich nur bei
        /// <see cref="ContinueOnError"/> = false: dort bricht der Lauf beim ersten Fehler ab, und von
        /// 1000 Elementen koennen 1 gescheitert, 4 fertig und <b>495 nie versucht</b> sein. Wer dann nur
        /// die gescheiterten wiederholt, verliert die 495 stillschweigend. Mit
        /// <see cref="ContinueOnError"/> = true sind beide Listen identisch (es bleibt nichts unversucht);
        /// <see cref="FailedItemsOutput"/> bleibt fuer Diagnose und Meldungen.
        /// </remarks>
        public string PendingItemsOutput { get; set; }

        /// <summary>
        /// Optional: welcher Ausgabeparameter des Einzeldurchlaufs das <b>fertige Element</b> ist (z.B.
        /// <c>ProcessedItem</c>). Er bestimmt, was in <see cref="SucceededItemsOutput"/> landet. Leer =
        /// dort steht das unveraenderte Eingabe-Element.
        /// </summary>
        public string ItemResultOutput { get; set; }

        /// <summary>
        /// Optional: der Ausgabeparameter, der die <b>erfolgreich verarbeiteten</b> Elemente aufnimmt -
        /// die Werte aus <see cref="ItemResultOutput"/> (ersatzweise die Eingabe-Elemente), ohne Luecken.
        /// Das Gegenstueck zu <see cref="PendingItemsOutput"/>: das eine ist das Erledigte, das andere das
        /// Offene. Leer = nicht sammeln.
        /// </summary>
        /// <remarks>
        /// Bewusst asymmetrisch zu <see cref="FailedItemsOutput"/>/<see cref="PendingItemsOutput"/>, die
        /// die <b>Eingabe</b>-Form fuehren: was noch zu tun ist, muss wieder in die Sammlung passen; was
        /// fertig ist, ist das Ergebnis. Erst dadurch passt eine Wiederholungs-Schleife zusammen.
        /// </remarks>
        public string SucceededItemsOutput { get; set; }

        /// <summary>
        /// Optional: der Name eines <b>Eingabe</b>parameters, dessen Elemente den neu erfolgreichen
        /// <b>vorangestellt</b> werden (gleiche Art wie <see cref="SucceededItemsOutput"/>). Damit traegt
        /// ein Wiederholungs-Durchlauf das Ergebnis der vorigen Durchlaeufe mit, statt es zu
        /// ueberschreiben - am Ende steht die vollstaendige Menge, obwohl jeder Durchlauf nur seinen Rest
        /// bearbeitet hat.
        /// </summary>
        /// <remarks>
        /// Wirkt <b>nur</b> auf <see cref="SucceededItemsOutput"/>, nicht auf die uebrigen
        /// Ergebnis-Listen: die bleiben streng „was DIESER Durchlauf erzeugt hat". null gilt als leer
        /// (der normale erste Durchlauf); eine Zeichenkette oder etwas nicht Aufzaehlbares ist ein Fehler.
        /// </remarks>
        public string CarryOverInput { get; set; }

        /// <summary>Ist die Iteration ueberhaupt benutzbar konfiguriert?</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ItemsInput);

        /// <summary>Der Eingabeparameter, unter dem ein Element uebergeben wird (siehe <see cref="ItemParameter"/>).</summary>
        public string EffectiveItemParameter =>
            string.IsNullOrWhiteSpace(ItemParameter) ? ItemsInput : ItemParameter;
    }
}
