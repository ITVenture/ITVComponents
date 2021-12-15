using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ParallelProcessing.TaskSchedulers;
using ITVComponents.WebCoreToolkit.BackgroundProcessing;

namespace ITVComponents.WebCoreToolkit.ScheduledBackgroundProcessing
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
