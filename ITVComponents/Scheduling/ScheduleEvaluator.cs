using System;
using System.Collections.Generic;

namespace ITVComponents.Scheduling
{
    /// <summary>
    /// Wertet ein <see cref="TimeTable"/>-Muster fuer Aufrufer aus, die ihren Zeitplan <b>nicht als
    /// lebendes Objekt halten</b>, sondern seinen Stand in einer Datenbank fuehren: naechste Faelligkeit
    /// in UTC, Pruefung des Musters und eine Vorschau fuer die Oberflaeche.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ortszeit rein, UTC raus.</b> Gerechnet wird in Ortszeit, weil ein Zeitplan das meint: "jeden Tag
    /// um 8" ist acht Uhr vor Ort, auch nach der Umstellung auf Sommerzeit - eine Umstellung verschoebe
    /// sonst jeden Termin um eine Stunde. Gespeichert und verglichen wird in UTC, weil das der Rest der
    /// Ablaufsteuerung tut.
    /// </para>
    /// <para>
    /// <b>Warum es diese Klasse gibt und nicht nur <see cref="TimeTable"/>:</b> deren
    /// <see cref="TimeTable.GetNextExecutionTime(DateTime)"/> traegt Zustand - das
    /// <c>t</c>-Kennzeichen ("erster Lauf sofort") wird beim ersten Aufruf verbraucht. Fuer einen
    /// Hintergrunddienst, der sein Objekt behaelt, ist das richtig. Wer den Plan je Auswertung neu aus der
    /// Datenbank baut, bekaeme damit bei jedem Durchgang "jetzt sofort" - also Dauerfeuer. Hier wird das
    /// Kennzeichen deshalb ueber den <b>gespeicherten letzten Lauf</b> aufgeloest: nur wenn es noch keinen
    /// gibt, ist "sofort" gemeint.
    /// </para>
    /// </remarks>
    public static class ScheduleEvaluator
    {
        /// <summary>
        /// Wie weit in die Zukunft hoechstens gesucht wird, bevor ein Muster als "trifft nie zu" gilt.
        /// </summary>
        /// <remarks>
        /// Ohne diese Grenze laeuft ein unerfuellbares Muster (etwa der 31. eines Monats, kombiniert mit
        /// Februar) in die Rekursion von <see cref="TimeTable"/> - und der Fehler faellt als haengender
        /// oder abstuerzender Dienst auf statt als das, was er ist: ein Tippfehler im Zeitplan.
        /// </remarks>
        public const int MaxLookAheadYears = 5;

        /// <summary>
        /// Die naechste Faelligkeit in UTC, oder null, wenn das Muster keinen weiteren Termin hergibt
        /// (einmaliger, bereits gelaufener Plan) oder innerhalb von <see cref="MaxLookAheadYears"/> keinen.
        /// </summary>
        /// <param name="pattern">das Zeitplan-Muster</param>
        /// <param name="lastRunUtc">
        /// der zuletzt ausgefuehrte Lauf (UTC), oder null, wenn dieser Plan noch nie gelaufen ist. Null ist
        /// zugleich die Bedingung, unter der ein Muster mit <c>t</c>-Kennzeichen "sofort" bedeutet.
        /// </param>
        /// <param name="nowUtc">der aktuelle Zeitpunkt (UTC)</param>
        /// <returns>die naechste Faelligkeit (UTC), oder null</returns>
        /// <exception cref="FormatException">wenn das Muster nicht lesbar ist</exception>
        public static DateTime? NextDueUtc(string pattern, DateTime? lastRunUtc, DateTime nowUtc)
        {
            var timeTable = new TimeTable(pattern);

            // "Sofort" gilt genau einmal - beim allerersten Mal. Danach entscheidet das Muster. So laesst
            // sich "laeuft an, sobald der Dienst da ist" abbilden, ohne dass daraus eine Endlosschleife
            // wird.
            if (timeTable.RunsImmediately && lastRunUtc == null)
            {
                return nowUtc;
            }

            if (timeTable.RunsImmediately)
            {
                // Den verbrauchbaren Merker leerlaufen lassen: der erste Aufruf liefert sonst "jetzt",
                // obwohl dieser Plan schon gelaufen ist. Bewusst hier und nicht in TimeTable selbst - dort
                // haengt das Verhalten an zwei produktiven Nutzern (Aufgaben-Verteiler, Hintergrunddienst).
                timeTable.GetNextExecutionTime(ToLocal(lastRunUtc ?? nowUtc), false);
            }

            DateTime anchorLocal = ToLocal(lastRunUtc ?? nowUtc);

            // Gerechnet wird zuerst ab dem letzten Lauf - damit ein Muster wie "jeden zweiten Tag" seinen
            // Rhythmus behaelt, statt bei jedem Aufruf neu anzusetzen. Ob das Ergebnis auch vor uns liegt,
            // entscheidet der Riegel weiter unten; TimeTable sichert das mit forceFutureDates:false NICHT
            // zu (die Begruendung steht dort).
            //
            // Nachgeholt wird ueber diesen Weg ohnehin nicht: das ergibt sich aus der GESPEICHERTEN
            // Faelligkeit. Sie bleibt stehen, solange niemand sie aufgreift, ist damit ueberfaellig und
            // feuert beim naechsten Aufgriff EINMAL - danach steht der Termin wieder ab jetzt. Genau so ist
            // "einmal nachholen, nicht n-mal" gemeint.
            DateTime? nextLocal = timeTable.GetNextExecutionTime(anchorLocal, false);
            if (nextLocal == null)
            {
                return null;
            }

            DateTime nextUtc = ToUtc(nextLocal.Value);

            // Und hier wird das Zukunfts-Versprechen dieser Methode tatsaechlich eingeloest - der Absatz
            // darueber beschrieb lange, was TimeTable NICHT tut.
            //
            // Mit forceFutureDates:false rechnet TimeTable ab dem Anker und baut den Termin AUS DEM
            // ANKERDATUM, sobald an jenem Tag noch eine Tageszeit uebrig ist (TimeTable.cs:315). Die
            // Rekursion, die Zukunft erzwingt, wird nur betreten, wenn dort KEINE mehr uebrig ist
            // (TimeTable.cs:164). Ein Anker von vor drei Tagen um 00:20 Ortszeit liefert deshalb den
            // Termin von vor drei Tagen um 08:00 - und niemand merkt es, solange alle Aufrufer als Anker
            // "jetzt" uebergeben. Genau das taten sie bisher; die Zusicherung hing an dieser Gewohnheit.
            //
            // Liegt das Ergebnis also nicht vor uns, wird ab JETZT gerechnet. Das ist die Bedeutung von
            // "naechste Faelligkeit" - und zugleich der Grund, warum ein langer Stillstand keine Kette
            // verpasster Termine erzeugt.
            //
            // Bewusst mit dem uebergebenen nowUtc und NICHT ueber die einparametrige Ueberladung: die
            // klemmt intern auf DateTime.Now (TimeTable.cs:128 -> :148) und koppelte diese Methode damit
            // von ihrem eigenen Zeit-Parameter ab - eine Zeitrechnung, die sich nicht mehr vorgeben
            // laesst, ist nicht pruefbar.
            if (nextUtc <= nowUtc)
            {
                nextLocal = timeTable.GetNextExecutionTime(ToLocal(nowUtc), false);
                if (nextLocal == null)
                {
                    return null;
                }

                nextUtc = ToUtc(nextLocal.Value);
            }

            if (nextUtc > nowUtc.AddYears(MaxLookAheadYears))
            {
                return null;
            }

            return nextUtc;
        }

        /// <summary>
        /// Prueft ein Muster, ohne zu werfen - fuer den Editor und die Validierung einer Definition.
        /// </summary>
        /// <param name="pattern">das zu pruefende Muster</param>
        /// <param name="error">die Fehlerbeschreibung, oder null</param>
        /// <returns>true, wenn das Muster lesbar ist UND innerhalb der Vorausschau einen Termin ergibt</returns>
        /// <remarks>
        /// Beides gehoert geprueft: ein Muster kann formal richtig sein und trotzdem nie zutreffen (der
        /// 30. Februar). Fiele nur das erste auf, meldete der Editor "in Ordnung" und der Zeitplan
        /// schwiege anschliessend fuer immer.
        /// </remarks>
        public static bool TryValidate(string pattern, out string error)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                error = "Es ist kein Zeitplan angegeben.";
                return false;
            }

            // ZUERST die strenge Zerlegung: die Auswertung liest ein Muster notfalls verkuerzt (ihr Regex
            // ist nicht verankert) und tut dann klaglos etwas anderes, als dasteht. Genau das soll hier
            // auffallen - im Editor, wo noch jemand davorsitzt.
            if (!SchedulePattern.TryParse(pattern, out _, out string structure))
            {
                error = structure;
                return false;
            }

            try
            {
                DateTime nowUtc = DateTime.UtcNow;
                if (NextDueUtc(pattern, nowUtc, nowUtc) == null)
                {
                    error = $"Der Zeitplan '{pattern}' ergibt in den naechsten {MaxLookAheadYears} Jahren "
                            + "keinen Termin.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Der Zeitplan '{pattern}' ist nicht lesbar: {ex.Message}";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Die naechsten <paramref name="count"/> Termine (UTC) - fuer die Vorschau im Editor
        /// ("wann laeuft das eigentlich?").
        /// </summary>
        /// <param name="pattern">das Muster</param>
        /// <param name="fromUtc">ab wann gerechnet wird (UTC)</param>
        /// <param name="count">wie viele Termine hoechstens</param>
        /// <returns>die Termine in aufsteigender Reihenfolge; leer, wenn das Muster keinen hergibt</returns>
        /// <remarks>
        /// Bricht ab, sobald kein Termin mehr kommt - eine Vorschau, die auf voller Laenge besteht, wuerde
        /// bei einem einmaligen Plan endlos suchen. Ein unlesbares Muster liefert eine leere Liste statt
        /// einer Ausnahme: die Vorschau ist eine Hilfe, kein Pruefwerkzeug (dafuer
        /// <see cref="TryValidate"/>).
        /// </remarks>
        public static IReadOnlyList<DateTime> Preview(string pattern, DateTime fromUtc, int count)
        {
            var result = new List<DateTime>();
            if (count <= 0 || string.IsNullOrWhiteSpace(pattern))
            {
                return result;
            }

            try
            {
                // Ein "sofort"-Kennzeichen wuerde die Vorschau mit dem aktuellen Zeitpunkt beginnen lassen
                // und damit ueber den eigentlichen Rhythmus nichts aussagen. Sie zeigt deshalb den Plan ab
                // dem Startpunkt - also so, als sei er schon einmal gelaufen.
                DateTime cursorUtc = fromUtc;
                for (int i = 0; i < count; i++)
                {
                    DateTime? next = NextDueUtc(pattern, cursorUtc, fromUtc);
                    if (next == null || next <= cursorUtc)
                    {
                        break;
                    }

                    result.Add(next.Value);
                    cursorUtc = next.Value;
                }
            }
            catch (Exception)
            {
                // Kein Log: die Vorschau laeuft an der Tastatur des Benutzers und sieht jedes halb
                // getippte Muster. Wer wissen will, WARUM eines nicht geht, ruft TryValidate - das sagt es
                // im Klartext.
                return result;
            }

            return result;
        }

        /// <summary>UTC nach Ortszeit - der Uebergang in die Welt, in der TimeTable rechnet.</summary>
        private static DateTime ToLocal(DateTime utc)
            => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

        /// <summary>
        /// Ortszeit nach UTC. <see cref="TimeTable"/> liefert einen Zeitpunkt ohne Kennzeichnung; er wird
        /// hier ausdruecklich als Ortszeit gelesen, sonst deutete <see cref="DateTime.ToUniversalTime"/>
        /// ihn je nach Herkunft unterschiedlich.
        /// </summary>
        private static DateTime ToUtc(DateTime local)
            => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
    }
}
