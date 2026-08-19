using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scheduling;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;

namespace ITVComponents.WebCoreToolkit.Extras.ScheduledBackgroundProcessing
{
    public class TimeTableBackgroundTask:BackgroundTask
    {
        public TimeTable TimeTable { get; set; }

        public DateTime NextExecution { get; set; }

        public TimeTableBackgroundTask Clone()
        {
            return new TimeTableBackgroundTask
            {
                AdditionalArguments = AdditionalArguments,
                ConservedContext = ConservedContext,
                Task = Task
            };
        }
    }
}
