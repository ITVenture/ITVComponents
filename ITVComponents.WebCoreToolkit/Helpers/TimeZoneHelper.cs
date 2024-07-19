using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Helpers
{
    public class TimeZoneHelper
    {
        private readonly TimeZoneInfo timeZone;

        /// <summary>
        /// Creates a new TimeZoneHelper object for the specified timeZoneInfo object
        /// </summary>
        /// <param name="targetTimeZone">the target zone</param>
        public TimeZoneHelper(TimeZoneInfo targetTimeZone)
        {
            this.timeZone = targetTimeZone;
        }

        /// <summary>
        /// Converts the given DateTime object to Utc depending on the TimeZone configured on the target tenant
        /// </summary>
        /// <param name="dateTimeValue">the dateTime value to convert</param>
        /// <returns>the utc-representation of the given datetime value</returns>
        public DateTime GetUtcDateTime(DateTime dateTimeValue)
        {
            var dt = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(dt, timeZone);
        }

        /// <summary>
        /// Converts the given DateTime object to localTime depending on the TimeZone configured on the target tenant
        /// </summary>
        /// <param name="dateTimeValue">the datetime value that represents a utc-datetime object</param>
        /// <returns>the local-time representation of the given datetime value</returns>
        DateTime GetLocalDateTime(DateTime dateTimeValue)
        {
            var dt = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTime(dt, timeZone);
        }
    }
}
