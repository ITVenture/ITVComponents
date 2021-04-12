using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ITVComponents.Threading
{
    /// <summary>
    /// Provides an Appdomain-wide Named-MutualExclusion mechanism
    /// </summary>
    public class NamedLock:IResourceLock
    {
        /// <summary>
        /// holds the internal mutex - object
        /// </summary>
        private object lockedObject;

        /// <summary>
        /// Holds a list of all mutexes that were created inside the current AppDomain
        /// </summary>
        private static ConcurrentDictionary<string, object> namedLocks = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the Mutex class
        /// </summary>
        /// <param name="name">the name of this named mutex object</param>
        public NamedLock(string name)
        {
            lockedObject = AcquireMutex(name);
            Monitor.Enter(lockedObject);
        }

        /// <summary>
        /// Initializes a new instance of the Mutext class
        /// </summary>
        /// <param name="name">the name of the named mutex</param>
        /// <param name="innerLock">the inner lock that will be released together with this mutex</param>
        public NamedLock(string name, IResourceLock innerLock) : this(name)
        {
            InnerLock = innerLock;
        }

        /// <summary>
        /// Gets the inner lock of this Resource Lock instance
        /// </summary>
        public IResourceLock InnerLock { get; }

        /// <summary>
        /// Awaits a Pluse on this mutexes inner lock
        /// </summary>
        public void Wait()
        {
            if (lockedObject != null)
            {
                Monitor.Wait(lockedObject);
            }
            else
            {
                throw new ObjectDisposedException("This mutex is no longer valid!");
            }
        }

        /// <summary>
        /// Awaits a Pluse on this mutexes inner lock for a specific timeout in Milliseconds.
        /// </summary>
        /// <param name="timeout">the timeout in milliseconds to wait for a pulse</param>
        /// <returns>a Value indicating whether the Pulse arrived whithin the provided timeout</returns>
        public bool Wait(int timeout)
        {
            if (lockedObject != null)
            {
                return Monitor.Wait(lockedObject, timeout);
            }

            throw new ObjectDisposedException("This mutex is no longer valid!");
        }

        /// <summary>
        /// Pulses this mutexes inner lock
        /// </summary>
        public void Pulse()
        {
            if (lockedObject != null)
            {
                Monitor.Pulse(lockedObject);
            }
            else
            {
                throw new ObjectDisposedException("This mutex is no longer valid!");
            }
        }

        /// <summary>
        /// Causes all waiting objects for this mutexes inner lock to receive a pulse signal
        /// </summary>
        public void PulseAll()
        {
            if (lockedObject != null)
            {
                Monitor.PulseAll(lockedObject);
            }
            else
            {
                throw new ObjectDisposedException("This mutex is no longer valid!");
            }
        }

        /// <summary>Führt anwendungsspezifische Aufgaben durch, die mit der Freigabe, der Zurückgabe oder dem Zurücksetzen von nicht verwalteten Ressourcen zusammenhängen.</summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (lockedObject != null)
            {
                Monitor.Exit(lockedObject);
                lockedObject = null;
            }

            InnerLock?.Dispose();
        }

        /// <summary>
        /// Creates a new Mutex-object that is bound to the given name
        /// </summary>
        /// <param name="name">the name of the named mutex</param>
        /// <returns>the object that is representing the named mutex</returns>
        private static object AcquireMutex(string name)
        {
            return namedLocks.GetOrAdd(name, s => new object());
        }
    }
}
