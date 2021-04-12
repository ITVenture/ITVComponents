using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing.Proxying
{
    [Serializable]
    internal class PackageTrigger
    {
        /// <summary>
        /// Gets or sets the package committment that was received from the worker service
        /// </summary>
        public PackageFinishedEventArgs Args { get; set; }

        /// <summary>
        /// Gets or sets the last trigger execution of this Trigger object
        /// </summary>
        public DateTime LastTrigger { get; set; }
    }
}
