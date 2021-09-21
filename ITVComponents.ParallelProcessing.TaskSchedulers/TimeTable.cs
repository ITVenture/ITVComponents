using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ITVComponents.DataAccess.Extensions;

namespace ITVComponents.ParallelProcessing.TaskSchedulers
{
    public class TimeTable
    {
        /// <summary>
        /// the Regex pattern that is used to analyze the period string
        /// </summary>
        private const string TimePatternRegex =
            @"(?<period>[odwmyhis])(?<firstDate>\d{8})(?<times>\d{4}(\;[\d\*]{4})*)(?<weekDays>(?:mon|tue|wed|thu|fri|sat|sun)*)(?<daysOfMonth>(?:01|02|03|04|05|06|07|08|09|10|11|12|13|14|15|16|17|18|19|20|21|22|23|24|25|26|27|28|29|30|31|-1)*)(?<months>(?:jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)*)(?<occurrence>\d{2})(?<desiredModulus>\.\d{2})?(?<firstRunImmediate>t)?";

        /// <summary>
        /// Gets the first date of execution
        /// </summary>
        private DateTime firstDate;

        /// <summary>
        /// all valid times whithin a day
        /// </summary>
        private static readonly string[] allTimes = BuildTimes();

        /// <summary>
        /// the period string that was used to initialize this TimeTable object
        /// </summary>
        private string pattern;

        /// <summary>
        /// an array containing all valid Execution Times
        /// </summary>
        private string[] validExecutionTimes;

        /// <summary>
        /// contains days on which the given timetable pattern string applies
        /// </summary>
        private string[] executionDays;

        /// <summary>
        /// contains days of a month on which the given timetable pattern string applies
        /// </summary>
        private string[] monthDaysOfExecution;

        /// <summary>
        /// contains months of a year on which the given timetable pattern string applies
        /// </summary>
        private string[] executionMonths;

        /// <summary>
        /// a modulus used to determine in which units the scheduler should occur
        /// </summary>
        private int unitModulus;

        /// <summary>
        /// the desired result of the provided modulus
        /// </summary>
        private int desiredUnitModulus;

        /// <summary>
        /// Defines the Period this TimeTable is based on
        /// </summary>
        private TimePeriod period;

        /// <summary>
        /// indicates whether to return the first date immediately
        /// </summary>
        private bool immediate;

        /// <summary>
        /// Initializes a new instance of the TimeTable class
        /// </summary>
        /// <param name="pattern">the timetable pattern that is used to generate a time-table</param>
        public TimeTable(string pattern)
        {
            this.pattern = pattern;
            ParsePattern();
        }

        /// <summary>
        /// Gets the next execution time based on the last execution of the configured plan
        /// </summary>
        /// <param name="lastExecution">the last time that the plan was executed</param>
        /// <returns>a datetime object representing the next point in time when the execution of the configured plan is required</returns>
        public DateTime? GetNextExecutionTime(DateTime lastExecution)
        {
            if (immediate)
            {
                immediate = false;
                return DateTime.Now;
            }

            bool includeMidnight = false;
            DateTime minExecution = DateTime.Now.Subtract(new TimeSpan(0, 0, 0, 15));
            if (lastExecution < minExecution)
            {
                lastExecution = minExecution;
            }

            if (lastExecution < firstDate || lastExecution.Date == lastExecution)
            {
                includeMidnight = true;
                if (lastExecution < firstDate)
                {
                    return firstDate;
                }
            }

            string time = lastExecution.ToString("HHmmss");
            string nextTime = (from t in validExecutionTimes where (!includeMidnight && t.CompareTo(time) > 0) || (includeMidnight && t.CompareTo(time) >= 0) select t).FirstOrDefault();
            DateTime? retVal;
            if (nextTime == null || period != TimePeriod.Day)
            {
                if (period == TimePeriod.Day)
                {
                    retVal =
                        GetNextExecutionTime(
                            lastExecution.Date.AddDays((unitModulus -
                                                        (((int) lastExecution.Day) + unitModulus)%unitModulus) +
                                                       desiredUnitModulus));
                }
                else if (period == TimePeriod.Week)
                {
                    DayOfWeek last = lastExecution.DayOfWeek;
                    int? offset;
                    DayOfWeek? next = GetNextDay(last, nextTime != null, out offset);
                    DateTime monday =
                            lastExecution.Subtract(new TimeSpan((int)lastExecution.DayOfWeek - 1, 0, 0, 0));
                    if (lastExecution.DayOfWeek == DayOfWeek.Sunday)
                    {
                        monday = monday.Subtract(new TimeSpan(7, 0, 0, 0));
                    }

                    int weekNumber = GetWeekNumber(monday);
                    if (time != null && next != null && next == last && weekNumber%unitModulus == desiredUnitModulus)
                    {
                        retVal = DateTime.ParseExact(string.Format("{0:yyyyMMdd} {1}", lastExecution, nextTime),
                                                     "yyyyMMdd HHmmss",
                                                     System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        next = GetNextDay(last, false, out offset);
                        if (next != null && offset != null && weekNumber % unitModulus == desiredUnitModulus)
                        {
                            retVal = GetNextExecutionTime(lastExecution.Date.AddDays(offset.Value));
                        }
                        else
                        {
                            int weekOffset = unitModulus - (weekNumber%unitModulus) + desiredUnitModulus;
                            int dayOffset = weekOffset*7;
                            monday = monday.AddDays(dayOffset);
                            next = GetNextDay(DayOfWeek.Monday, true, out offset);
                            if (next == null || offset == null)
                            {
                                throw new Exception(
                                    "No appropriate day was found for execution. Check Weekday - Configuration");
                            }

                            retVal = GetNextExecutionTime(monday.Date.AddDays(offset.Value));
                        }
                    }
                }
                else if (period == TimePeriod.Month)
                {
                    int day = lastExecution.Day;
                    int? nextDay = GetNextDay(lastExecution.Date, nextTime != null);
                    if (nextTime != null && nextDay != null && nextDay == day && lastExecution.Month%unitModulus == desiredUnitModulus)
                    {
                        retVal = DateTime.ParseExact(string.Format("{0:yyyyMMdd} {1}", lastExecution, nextTime),
                                                    "yyyyMMdd HHmmss",
                                                    System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        nextDay = GetNextDay(lastExecution.Date, false);
                        if (nextDay != null && lastExecution.Month % unitModulus == desiredUnitModulus)
                        {
                            retVal = GetNextExecutionTime(lastExecution.Date.AddDays(nextDay.Value - day));
                        }
                        else
                        {
                            DateTime tmp = NextMonthFirst(lastExecution.Month, lastExecution.Year);
                            nextDay = GetNextDay(tmp, true);
                            if (nextDay == null)
                            {
                                throw new Exception(
                                    "No appropriate day was found for execution. Check Monthday - Configuration");
                            }

                            retVal = GetNextExecutionTime(tmp.AddDays(nextDay.Value - 1));
                        }
                    }
                }
                else if (period == TimePeriod.Year)
                {
                    int day = lastExecution.Day;
                    int? nextDay = GetNextDay(lastExecution.Date, nextTime != null);
                    int? nextMonth = GetNextMonth(lastExecution.Month,true);
                    if (nextTime != null && nextDay != null && nextDay == day && lastExecution.Month == nextMonth &&
                        lastExecution.Year%unitModulus == desiredUnitModulus)
                    {
                        retVal = DateTime.ParseExact(string.Format("{0:yyyyMMdd} {1}", lastExecution, nextTime),
                                                    "yyyyMMdd HHmmss",
                                                    System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        nextDay = GetNextDay(lastExecution.Date, false);
                        if (nextDay != null && nextMonth != null && nextMonth == lastExecution.Month && lastExecution.Year % unitModulus == desiredUnitModulus)
                        {
                            retVal = GetNextExecutionTime(lastExecution.Date.AddDays(nextDay.Value - day));
                        }
                        else
                        {
                            nextMonth = GetNextMonth(lastExecution.Month, false);
                            if (nextMonth != null && lastExecution.Year % unitModulus == desiredUnitModulus)
                            {
                                DateTime tmp = new DateTime(lastExecution.Year, nextMonth.Value, 1);
                                nextDay = GetNextDay(tmp, true);
                                if (nextDay == null)
                                {
                                    throw new Exception(
                                        "No appropriate day was found for execution. Check Monthday - Configuration");
                                }

                                retVal = GetNextExecutionTime(tmp.AddDays(nextDay.Value - 1));
                            }
                            else
                            {
                                int yearOffset = unitModulus - (lastExecution.Year%unitModulus) + desiredUnitModulus;
                                int? month = GetNextMonth(0,false);
                                if (month == null)
                                {
                                    throw new Exception(
                                        "No appropriate month was found for execution. Check Month - Configuration");
                                }

                                DateTime tmp = new DateTime(lastExecution.Year + yearOffset, month.Value, 1);
                                nextDay = GetNextDay(tmp, true);
                                if (nextDay == null)
                                {
                                    throw new Exception(
                                        "No appropriate day was found for execution. Check Monthday - Configuration");
                                }

                                retVal = GetNextExecutionTime(tmp.AddDays(nextDay.Value - 1));
                            }
                        }
                    }
                }
                else if (period == TimePeriod.Once)
                {
                    return null;
                }
                else
                {
                    throw new Exception("Unknown Period chosen. Check configuration");
                }
            }
            else
            {
                retVal = DateTime.ParseExact(string.Format("{0:yyyyMMdd} {1}", lastExecution, nextTime), "yyyyMMdd HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            }

            return retVal;
        }

        /// <summary>
        /// Calculates the next valid month from the last month of execution
        /// </summary>
        /// <param name="month">the last month when a task was executed</param>
        /// <param name="includeLast">indicates whether the provided month may be re-used in the month-finding process</param>
        /// <returns>the next month in the same year</returns>
        private int? GetNextMonth(int month, bool includeLast)
        {
            var months = (from t in executionMonths
             select GetMonthValue(t)
             into monthVal
             where (monthVal > month) || (includeLast && monthVal >= month)
             orderby monthVal
             select monthVal).ToArray();
            if (months.Length > 0)
            {
                return months[0];
            }

            return null;
        }

        /// <summary>
        /// Gets the appropriate month value for a given name
        /// </summary>
        /// <param name="monthName">the short name of a specific month</param>
        /// <returns>the numeric value of the requested month</returns>
        private int GetMonthValue(string monthName)
        {
            switch (monthName.ToLower())
            {
                case "jan":
                    return 1;
                case "feb":
                    return 2;
                case "mar":
                    return 3;
                case "apr":
                    return 4;
                case "may":
                    return 5;
                case "jun":
                    return 6;
                case "jul":
                    return 7;
                case "aug":
                    return 8;
                case "sep":
                    return 9;
                case "oct":
                    return 10;
                case "nov":
                    return 11;
                case "dec":
                    return 12;
            }

            throw new Exception("Invalid month name provided");
        }

        /// <summary>
        /// Builds all minutes whithin 24 hours
        /// </summary>
        /// <returns>an array ordered by hour/minute</returns>
        private static string[] BuildTimes()
        {
            int[] minutes = new int[60];
            int[] hours = new int[24];
            int[] seconds = new int[60];
            return (from m in minutes.Select((v, i) => i)
                    join h in hours.Select((v, i) => i) on true equals true
                    join s in seconds.Select((v, i) => i) on true equals true
                    orderby h, m, s
                    select string.Format("{0:00}{1:00}{2:00}", h, m, s)).ToArray();
        }

        /// <summary>
        /// Gets the ISO8601 compilant WeekNumber of the given date
        /// </summary>
        /// <param name="date">the date for which to get the weekNumber</param>
        /// <returns>a ISO8601 compilant WeekNumber</returns>
        private static int GetWeekNumber(DateTime date)
        {
            DayOfWeek day = date.DayOfWeek;
            if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            {
                date = date.AddDays(3);
            }

            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek,
                                                                       DayOfWeek.Monday);
        }

        /// <summary>
        /// Gets the next start of a month from the month/year of the last execution
        /// </summary>
        /// <param name="month">the month of the last execution</param>
        /// <param name="year">the year of the last execution</param>
        /// <returns>the first day of the next month on which to schedule the planned event</returns>
        private DateTime NextMonthFirst(int month, int year)
        {
            int monthOffset = unitModulus - (month % unitModulus) + desiredUnitModulus;
            year += ((month + monthOffset) / 12);
            month = (month + monthOffset) % 12;
            if (month == 0)
            {
                month = 12;
                year -= 1;
            }

            return new DateTime(year, month, 1);
        }

        /// <summary>
        /// Gets the next Day of the current month on which the configured scheudle must apply
        /// </summary>
        /// <param name="last">the last schedule</param>
        /// <param name="includeLast">indicates whether to use a date that is greater-than (false) or greater-than-or-equal (true) to the provided date</param>
        /// <returns></returns>
        private int? GetNextDay(DateTime last, bool includeLast)
        {
            int dayOfMonth = last.Day;
            var furtherDays = (from t in monthDaysOfExecution
                               select GetAccurateDay(t, last)
                               into realDay
                               where (!includeLast && realDay > dayOfMonth) || (includeLast && realDay >= dayOfMonth)
                               orderby realDay
                               select realDay).ToArray();
            if (furtherDays.Length != 0)
            {
                return furtherDays[0];
            }

            return null;
        }

        /// <summary>
        /// Gets the accurate day for a specific day or a daycode (like -1) for a specific month
        /// </summary>
        /// <param name="dayOfMonth">the day for which to get the accurate representation</param>
        /// <param name="last">a specific day of that month</param>
        /// <returns>the representation as day of the provided month</returns>
        private int GetAccurateDay(string dayOfMonth, DateTime last)
        {
            int day = int.Parse(dayOfMonth);
            if (day == -1)
            {
                day = CultureInfo.InvariantCulture.Calendar.GetDaysInMonth(last.Year, last.Month);
            }

            return day;
        }

        /// <summary>
        /// Gets the next weekDay at which to execute the planned task
        /// </summary>
        /// <param name="last">the last day on which the execution has ocurred</param>
        /// <returns>a weekday of null, if no day of this week will apply to the given</returns>
        private DayOfWeek? GetNextDay(DayOfWeek last, bool includeFirst, out int? offset)
        {
            offset = null;
            int lastValue = (int) last;
            lastValue = (lastValue%7) + (((7 - (lastValue%7))/7)*7);
            var days = (from t in executionDays
                        select GetDayOfWeek(t)
                        into t2
                        select t2!=DayOfWeek.Sunday?(int) t2:7
                        into x
                        where (x > lastValue) || (x >= lastValue && includeFirst)
                        orderby (x%7) + (((7 - (x%7))/7)*7)
                        select new {Day=(DayOfWeek)(x%7), Val=x}).ToArray();
            if (days.Length != 0)
            {
                offset = days[0].Val - lastValue;
                return days[0].Day;
            }

            return null;
        }

        /// <summary>
        /// Gets the Weekday from a specific shortname
        /// </summary>
        /// <param name="identifier">the short identifier of a week</param>
        /// <returns>the DayOfWeek representation of the provided shortname</returns>
        private DayOfWeek GetDayOfWeek(string identifier)
        {
            switch (identifier.ToLower())
            {
                case "mon":
                    return DayOfWeek.Monday;
                case "tue":
                    return DayOfWeek.Tuesday;
                case "wed":
                    return DayOfWeek.Wednesday;
                case "thu":
                    return DayOfWeek.Thursday;
                case "fri":
                    return DayOfWeek.Friday;
                case "sat":
                    return DayOfWeek.Saturday;
                case "sun":
                    return DayOfWeek.Sunday;
            }

            throw new FormatException("Invalid day provided");
        }

        /// <summary>
        /// Parses the provided pattern and builds a timetable with valid execution times
        /// </summary>
        private void ParsePattern()
        {
            Match m = Regex.Match(pattern, TimePatternRegex,
                                  RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
                                  RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            if (m.Success)
            {
                unitModulus = int.Parse(m.Groups["occurrence"].Value);
                desiredUnitModulus = 0;
                immediate = m.Groups["firstRunImmediate"].Success;
                if (m.Groups["desiredModulus"].Success)
                {
                    desiredUnitModulus = int.Parse(m.Groups["desiredModulus"].Value.Substring(1));
                }

                period = GetPeriod(m.Groups["period"].Value);
                firstDate = DateTime.ParseExact(
                                    string.Format("{0} {1}", m.Groups["firstDate"].Value,
                                                  m.Groups["times"].Value.Substring(0, 4)),
                                    "yyyyMMdd HHmm", CultureInfo.InvariantCulture);
                if (period == TimePeriod.Second)
                {
                    validExecutionTimes = GetTimes(unitModulus,desiredUnitModulus, TimePeriod.Second);
                    period = TimePeriod.Day;
                    unitModulus = 1;
                    desiredUnitModulus = 0;
                }
                else if (period == TimePeriod.Minute)
                {
                    validExecutionTimes = GetTimes(unitModulus, desiredUnitModulus, TimePeriod.Minute);
                    period = TimePeriod.Day;
                    unitModulus = 1;
                    desiredUnitModulus = 0;
                }
                else if (period == TimePeriod.Hour)
                {
                    string[] tmp = ParseTimes(string.Format("**{0}", m.Groups["times"].Value.Substring(2)));
                    validExecutionTimes = (from t in tmp where int.Parse(t.Substring(0, 2))%unitModulus == desiredUnitModulus select t).ToArray();
                    period = TimePeriod.Day;
                    unitModulus = 1;
                    desiredUnitModulus = 0;
                }
                else
                {
                    this.validExecutionTimes = ParseTimes(m.Groups["times"].Value);
                    if (m.Groups["weekDays"].Success)
                    {
                        string days = m.Groups["weekDays"].Value;
                        if (days.Length%3 != 0)
                        {
                            throw new FormatException("Invalid WeekDays provided");
                        }

                        executionDays = new string[days.Length/3];
                        for (int i = 0, a=0; i < days.Length; i += 3, a++)
                        {
                            executionDays[a] = days.Substring(i, 3);
                        }
                    }

                    if (m.Groups["daysOfMonth"].Success)
                    {
                        string monthDays = m.Groups["daysOfMonth"].Value;
                        if (monthDays.Length%2 != 0)
                        {
                            throw new FormatException("Invalid MonthDays provided");
                        }

                        monthDaysOfExecution = new string[monthDays.Length/2];
                        for (int i = 0, a = 0; i < monthDays.Length; i += 2 , a++)
                        {
                            monthDaysOfExecution[a] = monthDays.Substring(i, 2);
                        }
                    }

                    if (m.Groups["months"].Success)
                    {
                        string months = m.Groups["months"].Value;
                        if (months.Length%3 != 0)
                        {
                            throw new FormatException("Invalid Months provided");
                        }

                        executionMonths = new string[months.Length/3];
                        for (int i = 0, a = 0; i < months.Length; i += 3, a++)
                        {
                            executionMonths[a] = months.Substring(i, 3);
                        }
                    }
                }
            }
            else
            {
                throw new FormatException("Invalid Period Format");
            }
        }

        /// <summary>
        /// Parses a time array of all valid times configured in the period configuration string
        /// </summary>
        /// <param name="configuredTimes">the configuration that was provided by the period config string</param>
        /// <returns>an array containing all times in a day that are supported for execution</returns>
        private string[] ParseTimes(string configuredTimes)
        {
            string[] times = configuredTimes.Split(';');
            List<string> supportedTimes = new List<string>();
            (from t in times
                       let a = from v in allTimes where IsFit(v, t) select v
                       where a.Count() != 0
                       select a.ToArray()).ForEach(supportedTimes.AddRange);
            return supportedTimes.Distinct().OrderBy(n => n).ToArray();
        }

        /// <summary>
        /// Gets all times that fit a specific modulus
        /// </summary>
        /// <param name="modulus">the value that was configured as minute modulus</param>
        /// <param name="desiredModulusResult">the desired result of the modulus operation</param>
        /// <param name="targetPeriod">the target period that is used for building the timetable</param>
        /// <returns>a string array containing all valid times</returns>
        private string[] GetTimes(int modulus, int desiredModulusResult, TimePeriod targetPeriod)
        {
            if (targetPeriod == TimePeriod.Minute)
            {
                modulus *= 60;
                desiredModulusResult *= 60;
            }
            else if (targetPeriod != TimePeriod.Second)
            {
                throw new InvalidOperationException(
                    "Invalid usage of GetTimes. Use with TimePeriod.Minute or TimePeriod.Second");
            }

            return (from t in allTimes.Select((val, idx) => new {Value = val, Index = idx})
             where t.Index%modulus == desiredModulusResult
             select t.Value).ToArray();
        }

        /// <summary>
        /// Indicates whether a specific time fits a provided pattern
        /// </summary>
        /// <param name="haystackTime">the time that was defined in an array</param>
        /// <param name="patternTime">the pattern time that was provided by the timetable pattern string</param>
        /// <returns>a value indicating whether the time fits the given pattern</returns>
        private bool IsFit(string haystackTime, string patternTime)
        {
            bool retVal=true;
            string compareTime = patternTime;
            if (!(haystackTime.Length == compareTime.Length))
            {
                compareTime += new string('0', haystackTime.Length - patternTime.Length);
            }

            for (int i = 0; i < haystackTime.Length; i++)
            {
                retVal &= (haystackTime[i] == compareTime[i] || compareTime[i] == '*');
                if (!retVal)
                {
                    break;
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets the appropriate TimePeriod value based on the configuration string
        /// </summary>
        /// <param name="periodIdentifier">the configured period identifier</param>
        /// <returns>a timeperiod indicating the period that is used for date calculations</returns>
        private TimePeriod GetPeriod(string periodIdentifier)
        {
            switch (periodIdentifier.ToLower())
            {
                case "o":
                    return TimePeriod.Once;
                case "s":
                    return TimePeriod.Second;
                case "i":
                    return TimePeriod.Minute;
                case "h":
                    return TimePeriod.Hour;
                case "d":
                    return TimePeriod.Day;
                case "w":
                    return TimePeriod.Week;
                case "m":
                    return TimePeriod.Month;
                case "y":
                    return TimePeriod.Year;

            }

            return TimePeriod.Undefined;
        }

        private enum TimePeriod
        {
            Once,
            Second,
            Minute,
            Hour,
            Day,
            Week,
            Month,
            Year,
            Undefined
        }
    }
}
