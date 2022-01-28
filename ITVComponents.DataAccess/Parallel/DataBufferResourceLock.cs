using System;
using ITVComponents.Threading;

namespace ITVComponents.DataAccess.Parallel
{
    /// <summary>
    /// Locks a database object with its resetEvent and releases it on disposal
    /// </summary>
    internal class DataBufferResourceLock:IResourceLock
    {
        /// <summary>
        /// The Queue into which to put the connection after disposal of this lock
        /// </summary>
        private DatabaseConnectionBuffer buffer;

        /// <summary>
        /// The target object that is used to cache the connection that is locked by this databufferlock
        /// </summary>
        private DatabaseContainer target;

        /// <summary>
        /// Initializes a new instance of the DataBufferResourceLock class
        /// </summary>
        /// <param name="buffer">The Queue into which to put the connection after disposal of this lock</param>
        /// <param name="target">The container object used to cache the connection</param>
        public DataBufferResourceLock(DatabaseConnectionBuffer buffer, DatabaseContainer target)
        {
            this.buffer = buffer;
            this.target = target;
        }

        /// <summary>
        /// Initializes a new instance of the DataBufferResourceLock class
        /// </summary>
        /// <param name="buffer">The Queue into which to put the connection after disposal of this lock</param>
        /// <param name="target">The container object used to cache the connection</param>
        /// <param name="innerLock">the inner lock of this resource lock</param>
        public DataBufferResourceLock(DatabaseConnectionBuffer buffer, DatabaseContainer target, IResourceLock innerLock)
            : this(buffer, target)
        {
            InnerLock = innerLock;
        }

        /// <summary>
        /// Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (InnerLock != null)
            {
                InnerLock.Dispose();
                InnerLock = null;
            }

            if (buffer != null)
            {
                buffer.DecreaseActiveConnectionCount(target);
                buffer = null;
            }
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get; private set; }

        public void Exclusive(Action action)
        {
            action();
        }

        public T Exclusive<T>(Func<T> action)
        {
            return action();
        }

        public IDisposable PauseExclusive()
        {
            return new ExclusivePauseHelper(()=>InnerLock?.PauseExclusive());
        }
    }
}
