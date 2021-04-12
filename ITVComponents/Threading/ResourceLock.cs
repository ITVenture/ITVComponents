using System.Threading;

namespace ITVComponents.Threading
{
    public class ResourceLock : IResourceLock
    {
        /// <summary>
        /// the resource object that is locked during the lifetime of this ResourceLock instance
        /// </summary>
        private object resource;

        /// <summary>
        /// the inner lock of this ResourceLock instance
        /// </summary>
        private IResourceLock innerLock;

        /// <summary>
        /// Initializes a new instance of the ResourceLock class
        /// </summary>
        /// <param name="resource">the resource to lock for synchronized access</param>
        /// <param name="innerLock">the inner lock which is to be unlocked as well as soon as this object disposes</param>
        public ResourceLock(object resource, IResourceLock innerLock)
            : this(resource)
        {
            this.innerLock = innerLock;
        }

        /// <summary>
        /// Initializes a new instance of the ResourceLock class
        /// </summary>
        /// <param name="resource">the resource to lock for synchronized access</param>
        public ResourceLock(object resource)
        {
            this.resource = resource;
            if (resource != null)
            {
                Monitor.Enter(resource);
            }
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get { return innerLock; } }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            if (innerLock != null)
            {
                innerLock.Dispose();
                innerLock = null;
            }

            if (resource != null)
            {
                Monitor.Exit(resource);
                resource = null;
            }
        }
    }
}