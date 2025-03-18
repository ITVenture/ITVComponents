using ITVComponents.Logging;
using System;
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
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get { return innerLock; } }

        public void Exclusive(bool autoLock, Action action)
        {
            if (InnerLock != null)
            {
                InnerLock.Exclusive(autoLock, () =>
                {
                    if (resource != null)
                    {
                        if (autoLock)
                        {
                            Monitor.Enter(resource);
                        }
                        try
                        {
                            action();
                        }
                        finally
                        {
                            if (autoLock && Monitor.IsEntered(resource))
                            {
                                Monitor.Exit(resource);
                            }
                        }
                    }
                    else
                    {
                        action();
                    }
                });
            }
            else
            {
                if (resource != null)
                {
                    if (autoLock)
                    {
                        Monitor.Enter(resource);
                    }
                    try
                    {
                        action();
                    }
                    finally
                    {
                        if (autoLock && Monitor.IsEntered(resource))
                        {
                            Monitor.Exit(resource);
                        }
                    }
                }
                else
                {
                    action();
                }
            }
        }

        public T Exclusive<T>(bool autoLock, Func<T> action)
        {
            if (InnerLock != null)
            {
                return InnerLock.Exclusive(autoLock, () =>
                {
                    if (resource != null)
                    {
                        if (autoLock)
                        {
                            Monitor.Enter(resource);
                        }
                        try
                        {
                            return action();
                        }
                        finally
                        {
                            if (autoLock && Monitor.IsEntered(resource))
                            {
                                Monitor.Exit(resource);
                            }
                        }
                    }

                    return action();
                });
            }
            else
            {
                if (resource != null)
                {
                    if (autoLock)
                    {
                        Monitor.Enter(resource);
                    }
                    try
                    {
                        return action();
                    }
                    finally
                    {
                        if (autoLock && Monitor.IsEntered(resource))
                        {
                            Monitor.Exit(resource);
                        }
                    }
                }

                return action();
            }
        }

        public void SynchronizeContext()
        {
            innerLock?.SynchronizeContext();
            if (resource != null)
            {
                Monitor.Enter(resource);
            }
        }

        public void LeaveSynchronizeContext()
        {
            if (resource != null && Monitor.IsEntered(resource))
            {
                Monitor.Exit(resource);
            }
            else
            {
                LogEnvironment.LogEvent("LeaveSynchronized was called without a synchronized context!", LogSeverity.Warning);
            }

            innerLock?.LeaveSynchronizeContext();
        }

        public IDisposable PauseExclusive()
        {
            return new ExclusivePauseHelper(() => InnerLock?.PauseExclusive(),resource);
        }

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

            resource = null;
        }
    }
}