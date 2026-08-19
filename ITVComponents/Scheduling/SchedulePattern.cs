using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ITVComponents.Scheduling
{
    /// <summary>Die Periode, in der ein Zeitplan wiederkehrt.</summary>
    public enum SchedulePeriod
    {
        /// <summary>Einmalig.</summary>
        Once,

        /// <summary>Sekuendlich (im angegebenen Takt).</summary>
        Second,

        /// <summary>Minuetlich (im angegebenen Takt).</summary>
        Minute,

        /// <summary>Stuendlich (im angegebenen Takt).</summary>
        Hour,

        /// <summary>Taeglich.</summary>
        Day,

        /// <summary>Woechentlich - dann zaehlen die Wochentage.</summary>
        Week,

        /// <summary>Monatlich - dann zaehlen die Monatstage.</summary>
        Month,

        /// <summary>Jaehrlich - dann zaehlen Monatstage und Monate.</summary>
        Year
    }

    /// <summary>
    /// Die <b>zerlegte</b> Form eines <see cref="TimeTable"/>-Musters: lesbar, veraenderbar und wieder
    /// zusammensetzbar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es gibt sie, weil das Muster ein <b>Maschinenformat</b> ist - <c>d20200101080001</c> tippt niemand
    /// von Hand und liest niemand ab. Eine Oberflaeche, die es zusammenklicken laesst, braucht beide
    /// Richtungen: Muster aufmachen und Muster bauen. <see cref="TimeTable"/> selbst kann nur die
    /// Auswertung; ihr Zerlegen ist privat und schreibt in Felder.
    /// </para>
    /// <para>
    /// Bewusst hier neben <see cref="TimeTable"/> und nicht in der Oberflaeche: sonst haette der Designer
    /// seinen eigenen Parser, und der liefe frueher oder spaeter anders als die Auswertung - still, und
    /// erst zu bemerken, wenn ein Zeitplan zu einer anderen Zeit laeuft, als im Editor stand. Aus
    /// demselben Grund benutzt <see cref="TryParse"/> denselben Regex wie die Auswertung.
    /// </para>
    /// </remarks>
    public sealed class SchedulePattern
    {
        private static readonly string[] WeekDayCodes =
            { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

        private static readonly string[] MonthCodes =
            { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };

        /// <summary>Die Periode.</summary>
        public SchedulePeriod Period { get; set; } = SchedulePeriod.Day;

        /// <summary>
        /// Der <b>Anker</b>: ab wann der Plan gilt. Sein Datum ist der Startpunkt, seine Uhrzeit die
        /// erste (und bei Sekunden/Minuten/Stunden die einzige) Ausfuehrungszeit.
        /// </summary>
        public DateTime FirstDate { get; set; } = DateTime.Today;

        /// <summary>
        /// Die weiteren Uhrzeiten als <c>HHmm</c>, jeweils vierstellig. <c>*</c> steht fuer "beliebig"
        /// (<c>**30</c> = jede Stunde zur Minute 30). Die ERSTE Uhrzeit steht in
        /// <see cref="FirstDate"/> - so verlangt es das Format.
        /// </summary>
        public List<string> AdditionalTimes { get; set; } = new List<string>();

        /// <summary>Bei <see cref="SchedulePeriod.Week"/>: an welchen Wochentagen.</summary>
        public List<DayOfWeek> WeekDays { get; set; } = new List<DayOfWeek>();

        /// <summary>
        /// Bei <see cref="SchedulePeriod.Month"/>/<see cref="SchedulePeriod.Year"/>: an welchen Tagen des
        /// Monats. <b>-1 steht fuer den LETZTEN Tag des Monats</b> - das, was Cron nicht kann.
        /// </summary>
        public List<int> DaysOfMonth { get; set; } = new List<int>();

        /// <summary>Bei <see cref="SchedulePeriod.Year"/>: in welchen Monaten (1-12).</summary>
        public List<int> Months { get; set; } = new List<int>();

        /// <summary>
        /// Der <b>Takt</b>: jede n-te Einheit der Periode (1 = jede). Bei Sekunden/Minuten/Stunden ist es
        /// der eigentliche Rhythmus ("alle 15 Minuten").
        /// </summary>
        public int Occurrence { get; set; } = 1;

        /// <summary>
        /// Der gewuenschte <b>Rest</b> zum Takt, oder null. Damit laesst sich sagen, WELCHE der n-ten
        /// Einheiten gemeint ist - "jede 2. Woche, und zwar die geraden" ist Takt 2 mit Rest 0. Ohne
        /// Angabe gilt 0.
        /// </summary>
        public int? DesiredModulus { get; set; }

        /// <summary>
        /// Das <c>t</c>-Kennzeichen: der erste Lauf sofort, sobald es den Plan gibt. Greift genau
        /// einmal - solange der Plan noch nie gelaufen ist.
        /// </summary>
        public bool RunFirstImmediately { get; set; }

        /// <summary>
        /// Zerlegt ein Muster. Liefert false mit einer Klartext-Begruendung, wenn es nicht lesbar ist.
        /// </summary>
        /// <param name="pattern">das Muster</param>
        /// <param name="result">die zerlegte Form, oder null</param>
        /// <param name="error">die Begruendung, oder null</param>
        public static bool TryParse(string pattern, out SchedulePattern result, out string error)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                error = "Es ist kein Zeitplan angegeben.";
                return false;
            }

            // VERANKERT - anders als bei der Auswertung. Der gemeinsame Regex hat kein ^...$, und ohne
            // Anker greift Regex.Match den kuerzesten passenden ANFANG und laesst den Rest liegen:
            // "m20200101080001-101" wird dann klaglos als "m20200101080001" gelesen, und der Plan laeuft
            // zu einer anderen Zeit, als dasteht. Fuer die Zerlegung ist das nicht hinnehmbar - sie ist
            // die Grundlage dessen, was der Editor anzeigt.
            Match m = Regex.Match(pattern.Trim(), $"^(?:{TimeTable.TimePatternRegex})$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
                | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            if (!m.Success)
            {
                error = $"'{pattern}' entspricht nicht dem Aufbau eines Zeitplans. Die Teile stehen in "
                        + "fester Reihenfolge: Periode, Startdatum, Zeiten, Wochentage, Monatstage, "
                        + "Monate, Takt.";
                return false;
            }

            var parsed = new SchedulePattern
            {
                Period = PeriodOf(m.Groups["period"].Value),
                Occurrence = int.Parse(m.Groups["occurrence"].Value, CultureInfo.InvariantCulture),
                RunFirstImmediately = m.Groups["firstRunImmediate"].Success
            };

            if (m.Groups["desiredModulus"].Success)
            {
                parsed.DesiredModulus = int.Parse(m.Groups["desiredModulus"].Value.Substring(1),
                    CultureInfo.InvariantCulture);
            }

            string[] times = m.Groups["times"].Value.Split(';');
            if (!DateTime.TryParseExact($"{m.Groups["firstDate"].Value} {times[0]}", "yyyyMMdd HHmm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime anchor))
            {
                // Die erste Zeit darf laut Regex Platzhalter enthalten; als Anker taugt sie dann nicht.
                error = $"'{times[0]}' ist als erste Uhrzeit nicht lesbar.";
                return false;
            }

            parsed.FirstDate = anchor;
            parsed.AdditionalTimes = times.Skip(1).ToList();
            parsed.WeekDays = Chunks(m.Groups["weekDays"].Value, 3)
                .Select(c => (DayOfWeek)Array.IndexOf(WeekDayCodes, c.ToLowerInvariant()))
                .Where(d => (int)d >= 0)
                .ToList();
            parsed.DaysOfMonth = Chunks(m.Groups["daysOfMonth"].Value, 2)
                .Select(c => int.Parse(c, CultureInfo.InvariantCulture))
                .ToList();
            parsed.Months = Chunks(m.Groups["months"].Value, 3)
                .Select(c => Array.IndexOf(MonthCodes, c.ToLowerInvariant()) + 1)
                .Where(i => i > 0)
                .ToList();

            result = parsed;
            error = null;
            return true;
        }

        /// <summary>
        /// Setzt das Muster wieder zusammen - die Gegenrichtung zu <see cref="TryParse"/>.
        /// </summary>
        public string ToPattern()
        {
            var sb = new StringBuilder();
            sb.Append(CodeOf(Period));
            sb.Append(FirstDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            sb.Append(FirstDate.ToString("HHmm", CultureInfo.InvariantCulture));

            foreach (string time in AdditionalTimes ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(time))
                {
                    sb.Append(';').Append(time.Trim());
                }
            }

            // Die Reihenfolge der Bloecke ist NICHT beliebig - der Regex liest sie genau so. Deshalb hier
            // fest verdrahtet und nicht aus einer Schleife ueber die gesetzten Angaben.
            foreach (DayOfWeek day in (WeekDays ?? new List<DayOfWeek>()).Distinct().OrderBy(d => (int)d))
            {
                sb.Append(WeekDayCodes[(int)day]);
            }

            // Der letzte Tag (-1) kommt ans ENDE, nicht an den Anfang: aufsteigend sortiert stuende er
            // vor dem Ersten, und "am 1. und am letzten" laese sich als "am letzten und am 1." - dieselbe
            // Bedeutung, aber ein anderer String, und damit eine Aenderung am gespeicherten Muster, die
            // niemand vorgenommen hat.
            foreach (int day in (DaysOfMonth ?? new List<int>()).Distinct()
                         .OrderBy(d => d == -1 ? int.MaxValue : d))
            {
                sb.Append(day == -1 ? "-1" : day.ToString("00", CultureInfo.InvariantCulture));
            }

            foreach (int month in (Months ?? new List<int>()).Distinct().OrderBy(mo => mo))
            {
                if (month >= 1 && month <= 12)
                {
                    sb.Append(MonthCodes[month - 1]);
                }
            }

            sb.Append(Math.Max(1, Occurrence).ToString("00", CultureInfo.InvariantCulture));
            if (DesiredModulus.HasValue)
            {
                sb.Append('.').Append(DesiredModulus.Value.ToString("00", CultureInfo.InvariantCulture));
            }

            if (RunFirstImmediately)
            {
                sb.Append('t');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ob diese Angaben zusammenpassen - was die Oberflaeche <b>vor</b> dem Zusammenbau sagen kann.
        /// </summary>
        /// <param name="error">die Begruendung, oder null</param>
        /// <remarks>
        /// Geprueft wird nur, was sich aus den Angaben selbst ergibt (eine Woche ohne Wochentag trifft
        /// nie zu). Ob ein formal stimmiges Muster tatsaechlich je einen Termin ergibt, beantwortet
        /// <see cref="ScheduleEvaluator.TryValidate"/> - dafuer muss gerechnet werden.
        /// </remarks>
        public bool IsConsistent(out string error)
        {
            if (Period == SchedulePeriod.Week && (WeekDays == null || WeekDays.Count == 0))
            {
                error = "Ein woechentlicher Plan braucht mindestens einen Wochentag.";
                return false;
            }

            if ((Period == SchedulePeriod.Month || Period == SchedulePeriod.Year)
                && (DaysOfMonth == null || DaysOfMonth.Count == 0))
            {
                error = "Ein monatlicher oder jaehrlicher Plan braucht mindestens einen Tag im Monat.";
                return false;
            }

            if (Period == SchedulePeriod.Year && (Months == null || Months.Count == 0))
            {
                error = "Ein jaehrlicher Plan braucht mindestens einen Monat.";
                return false;
            }

            if (Occurrence < 1)
            {
                error = "Der Takt muss mindestens 1 sein.";
                return false;
            }

            if (DesiredModulus.HasValue && DesiredModulus.Value >= Occurrence)
            {
                // Ein Rest, der so gross ist wie der Takt, kommt nie heraus - der Plan schwiege fuer immer.
                error = $"Der Rest muss kleiner sein als der Takt ({Occurrence}).";
                return false;
            }

            error = null;
            return true;
        }

        private static IEnumerable<string> Chunks(string value, int size)
        {
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }

            for (int i = 0; i + size <= value.Length; i += size)
            {
                yield return value.Substring(i, size);
            }
        }

        private static SchedulePeriod PeriodOf(string code)
            => code?.ToLowerInvariant() switch
            {
                "o" => SchedulePeriod.Once,
                "s" => SchedulePeriod.Second,
                "i" => SchedulePeriod.Minute,
                "h" => SchedulePeriod.Hour,
                "w" => SchedulePeriod.Week,
                "m" => SchedulePeriod.Month,
                "y" => SchedulePeriod.Year,
                _ => SchedulePeriod.Day
            };

        private static string CodeOf(SchedulePeriod period)
            => period switch
            {
                SchedulePeriod.Once => "o",
                SchedulePeriod.Second => "s",
                SchedulePeriod.Minute => "i",
                SchedulePeriod.Hour => "h",
                SchedulePeriod.Week => "w",
                SchedulePeriod.Month => "m",
                SchedulePeriod.Year => "y",
                _ => "d"
            };
    }
}
