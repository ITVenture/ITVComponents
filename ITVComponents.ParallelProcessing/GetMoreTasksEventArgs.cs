using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    /// <summary>
    /// EventArguments for EventHandler&lt;GetMoretaskEventArgs&gt; events
    /// </summary>
    public class GetMoreTasksEventArgs:EventArgs
    {
        /// <summary>
        /// Gets or sets of which to add new TaskItems
        /// </summary>
        public int Priority { get; set; }
    }
}
