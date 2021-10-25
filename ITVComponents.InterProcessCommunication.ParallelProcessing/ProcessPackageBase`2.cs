using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.Extensions;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.ParallelProcessing
{
    public abstract class ProcessPackageBase<TTask, TMe> : IProcessPackage, ISerializable where TTask : ProcessTaskBase<TTask, TMe>
                                                                                        where TMe : ProcessPackageBase<TTask, TMe>
    {
        /// <summary>
        /// The SubTasks associated with this ReleaseJob
        /// </summary>
        private TTask[] subTasks;

        /// <summary>
        /// the number of open tasks in this release job
        /// </summary>
        private int openTaskCount;

        /// <summary>
        /// the resource lock of this release job
        /// </summary>
        private object localResourceLock = new object();

        /// <summary>
        /// Prevents a default instance of the ExternalPackage class from being created
        /// </summary>
        protected ProcessPackageBase()
        {
            Sync = new object();
            CreationTime = DateTime.Now;
        }

        protected ProcessPackageBase(SerializationInfo info, StreamingContext context)
        {
            Id = (int)info.GetValue("Id", typeof(int));
            PackagePriority = (int)info.GetValue("PackagePriority", typeof(int));
            RequestingSystem = (string)info.GetValue("RequestingSystem", typeof(string));
            Successful = (bool)info.GetValue("Successful", typeof(bool));
            CreationTime = (DateTime)info.GetValue("CreationTime", typeof(DateTime));
            subTasks = (TTask[])info.GetValue("subTasks", typeof(TTask[]));
            openTaskCount = (int)info.GetValue("openTaskCount", typeof(int));
            if (subTasks != null)
            {
                subTasks.ForEach(n => n.SetParent((TMe)this));
            }
        }

        /// <summary>
        /// Gets an object that enables client objects to Synchronize calls on this object
        /// </summary>
        public object Sync { get; private set; }

        /// <summary>
        /// Sets the external tasks that need to be processed by a remote service
        /// </summary>
        /// <param name="tasks">the processable tasks</param>
        protected void SetSubTasks(TTask[] tasks)
        {
            subTasks = tasks;
            openTaskCount = subTasks.Length;
            foreach (var t in subTasks)
            {
                if (t.Parent == null)
                {
                    t.SetParent((TMe)this);
                }
            }
        }

        /// <summary>
        /// The Id of the current Job
        /// </summary>
        public int Id { get; protected set; }

        /// <summary>
        /// The Priority of the current job
        /// </summary>
        public int PackagePriority { get; protected set; }

        /// <summary>
        /// The System identifier of the requesting system
        /// </summary>
        public string RequestingSystem { get; set; }

        /// <summary>
        /// Gets a value indicating whether the processing was successful
        /// </summary>
        public bool Successful { get; protected set; }

        /// <summary>
        /// Gets the excact date and time when this package has been created
        /// </summary>
        public DateTime CreationTime { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether this package contains tasks
        /// </summary>
        public bool HasTasks { get { return subTasks.Length != 0; } }

        /// <summary>
        /// Gets the tasks that are provided for this Process package
        /// </summary>
        /// <returns>the tasks for this package</returns>
        public IProcessTask[] GetTasks()
        {
            EnsureTasks();
            return subTasks.Cast<IProcessTask>().ToArray();
        }

        /// <summary>
        /// Decreases the number of open items and raises the PackageFinished event when done
        /// </summary>
        /// <param name="success">indicates whether the processing of the current task was successful</param>
        /// <param name="ex">the exception that has ocurred during processing</param>
        public void DecreaseOpenItems(bool success, SerializedException ex)
        {
            lock (localResourceLock)
            {
                if (openTaskCount > 0)
                {
                    Successful &= success;
                    openTaskCount--;
                    if (ex != null)
                    {
                        LogEnvironment.LogEvent(ex.ToString(), LogSeverity.Error);
                    }

                    if (openTaskCount == 0)
                    {
                        OnPackageFinished(new PackageFinishedEventArgs
                        {
                            Package = this,
                            Tasks = subTasks.Cast<IProcessTask>().ToArray()
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Demands the queue that is processing this job for arequeue of a specific item
        /// </summary>
        /// <param name="taskItem">the task that needs to be re-rpocessed</param>
        /// <returns>a value indicating whether the requeue-demand was accepted by the processor</returns>
        protected internal virtual bool DemandRequeue(TTask taskItem)
        {
            if (DemandForRequeue != null)
            {
                return DemandForRequeue(this, taskItem);
            }

            return false;
        }

        /// <summary>
        /// Allows a derived class to act on an exception
        /// </summary>
        /// <param name="exception">the serialized exception containing information about what was going wrong</param>
        protected virtual void OnException(SerializedException exception)
        {
        }

        /// <summary>
        /// Enables a derived class to set the tasks of this package before they are enumerated
        /// </summary>
        protected virtual void EnsureTasks()
        {

        }

        /// <summary>
        /// Raises the PackageFinished event
        /// </summary>
        /// <param name="e">the arguments of the finished event</param>
        protected virtual void OnPackageFinished(PackageFinishedEventArgs e)
        {
            if (PackageFinished != null)
            {
                PackageFinished(this, e);
            }
        }

        /// <summary>
        /// Informs a client object that this Package has successfully processed
        /// </summary>
        [field: NonSerialized]
        public event PackageFinishedEventHandler PackageFinished;

        /// <summary>
        /// Informs a client object that a task of this Package demands to be re-queued
        /// </summary>
        [field: NonSerialized]
        public event DemandForRequeueEventHandler DemandForRequeue;

        /// <summary>Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with the data needed to serialize the target object.</summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> to populate with data.</param>
        /// <param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext" />) for this serialization.</param>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Id", Id);
            info.AddValue("PackagePriority", PackagePriority);
            info.AddValue("RequestingSystem", RequestingSystem);
            info.AddValue("Successful", Successful);
            info.AddValue("CreationTime", CreationTime);
            info.AddValue("subTasks", subTasks);
            info.AddValue("openTaskCount", openTaskCount);
            CompleteObjectData(info, context);
        }

        /// <summary>Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo" /> with the data needed to serialize the target object.</summary>
        /// <param name="info">The <see cref="T:System.Runtime.Serialization.SerializationInfo" /> to populate with data.</param>
        /// <param name="context">The destination (see <see cref="T:System.Runtime.Serialization.StreamingContext" />) for this serialization.</param>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
        protected virtual void CompleteObjectData(SerializationInfo info, StreamingContext context)
        {
        }
    }
}
