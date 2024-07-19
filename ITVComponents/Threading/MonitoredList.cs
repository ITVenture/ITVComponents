using System;
using System.Collections;
using System.Threading;

namespace ITVComponents.Threading
{
    /// <summary>
    /// Removes failed calls that have become abandoned
    /// </summary>
    public class MonitoredList<TContainerList>:IDisposable where TContainerList:IEnumerable
    {
        /// <summary>
        /// Locks this instance for multithreaded access
        /// </summary>
        private object locker = new object();

        /// <summary>
        /// the list to monitor
        /// </summary>
        private TContainerList list;

        /// <summary>
        /// The monitor method that periodically is fired to check the list
        /// </summary>
        private MonitorMethod<TContainerList> monitor;

        /// <summary>
        /// Triggers the monitoring method for the list
        /// </summary>
        private Timer monitorTrigger;

        /// <summary>
        /// the timeout after which te monitoring must be triggered periodically
        /// </summary>
        private long timeout;

        /// <summary>
        /// Initializes a new instance of the MonitoredList class
        /// </summary>
        /// <param name="monitor">the monitor method used to monitor the target list</param>
        /// <param name="list">the list that is wrapped by this instance</param>
        /// <param name="timeout">the timeout after which the monitoring must be triggered periodically</param>
        public MonitoredList(MonitorMethod<TContainerList> monitor, TContainerList list, long timeout):this()
        {
            this.timeout = timeout;
            this.list = list;
            this.monitor = monitor;
            monitorTrigger = new Timer((o) =>
                                           {
                                               monitorTrigger.Change(Timeout.Infinite, Timeout.Infinite);
                                               o.LocalOwner(o.ToString());
                                               try
                                               {
                                                   TContainerList l;
                                                   using (var lk = AcquireList(out l))
                                                   {
                                                       lk.Exclusive(true, ()=>this.monitor(l));
                                                   }
                                               }
                                               finally
                                               {
                                                   monitorTrigger.Change(this.timeout, this.timeout);
                                                   o.LocalOwner(null);
                                               }
                                           }, string.Format("::{0}::", GetHashCode()), Timeout.Infinite,
                                         Timeout.Infinite);
            monitorTrigger.Change(timeout, timeout);
        }  

        /// <summary>
        /// Prevents a default instance of the 
        /// </summary>
        private MonitoredList()
        {
        }

        /// <summary>
        /// Gets a locked instance of the inner list
        /// </summary>
        /// <param name="list">the list that is wrapped by this instance</param>
        /// <returns>the resource lock for the wrapped list</returns>
        public IResourceLock AcquireList(out TContainerList list)
        {
            list = this.list;
            return new ResourceLock(locker);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public virtual void Dispose()
        {
            monitorTrigger.Dispose();
        }
    }

    /// <summary>
    /// Delegate method used to monitor items in a list and remove items that are too old or do other monitoring tasks on them
    /// </summary>
    /// <typeparam name="TContainerList">the container list type that is monitored by this MonitorMethod</typeparam>
    /// <param name="list">the list that is monitored</param>
    public delegate void MonitorMethod<in TContainerList>(TContainerList list);
}
