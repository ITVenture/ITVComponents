using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.ScheduledBackgroundProcessing.Options
{
    public class ScheduledTasksOption
    {
        private List<Action<ITimeTableBackgroundTaskQueue>> queueConfigurations = new List<Action<ITimeTableBackgroundTaskQueue>>();

        public void ConfigureTasks(Action<ITimeTableBackgroundTaskQueue> configure)
        {
            queueConfigurations.Add(configure);
        }

        internal void Configure(ITimeTableBackgroundTaskQueue queue)
        {
            queueConfigurations.ForEach(n => n(queue));
        }
    }
}
