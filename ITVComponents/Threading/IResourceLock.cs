using System;
using System.Threading.Tasks;

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

        /// <summary>
        /// Uses the locked object to perform an action exclusively
        /// </summary>
        /// <param name="action">the action to perform with a sync-lock</param>
        void Exclusive(Action action);

        /// <summary>
        /// Uses the locked object to perform an action exclusively
        /// </summary>
        /// <param name="action">the action to perform with a sync-lock</param>
        T Exclusive<T>(Func<T> action);

        IDisposable PauseExclusive();
    }
}