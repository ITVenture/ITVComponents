using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing.Proxying
{
    [Serializable]
    internal class PackageSender
    {
        /// <summary>
        /// Indicates whether this package has already been sent
        /// </summary>
        public bool Sent { get; set; }

        /// <summary>
        /// The Package that must be sent to the signerservice
        /// </summary>
        public IProcessPackage Package { get; set; }
    }
}
