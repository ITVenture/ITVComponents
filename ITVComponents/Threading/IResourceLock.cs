using System;

namespace ITVComponents.Threading
{
    /// <summary>
    /// Describes a resource locking object that is capable for synchronization actions between threads
    /// </summary>
    public interface IResourceLock : IDisposable
    {
        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        IResourceLock InnerLock { get; }
    }
}