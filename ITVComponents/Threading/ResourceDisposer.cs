using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Threading
{
    public class ResourceDisposer:ResourceLock
    {
        private IDisposable localResource;

        /// <summary>
        /// Initializes a new instance of the ResourceDisposer class
        /// </summary>
        /// <param name="resource">the resource that is used for one-time - access</param>
        /// <param name="innerLock">the inner lock that may lock further objects</param>
        public ResourceDisposer(IDisposable resource, IResourceLock innerLock) : base(resource, innerLock)
        {
            localResource = resource;
        }

        /// <summary>
        /// Initializes a new instance of the ResourceDisposer class
        /// </summary>
        /// <param name="resource">The resource that is used for one-time access</param>
        public ResourceDisposer(IDisposable resource) : base(resource)
        {
            localResource = resource;
        }

        #region Overrides of ResourceLock

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            base.Dispose();
            localResource?.Dispose();
            localResource = null;
        }

        #endregion
    }
}
