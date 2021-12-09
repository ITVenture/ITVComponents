using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.BackgroundProcessing
{
    /// <summary>
    /// A Task that will be processed in a background thrad by the service-worker
    /// </summary>
    public class BackgroundTask
    {
        /// <summary>
        /// Gets or sets an async callback that will be called from the service-worker
        /// </summary>
        public Func<BackgroundTaskContext, object?, Task> Task { get; set; }

        /// <summary>
        /// Gets or sets the Conserved ActionContext that can be collected in any context with a present HttpContext
        /// </summary>
        public object ConservedContext { get; set; }

        /// <summary>
        /// Holds additional arguments that will be used for the Backgroundexecuted task
        /// </summary>
        public object AdditionalArguments { get; set; }
    }
}
