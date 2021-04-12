using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.ParallelProcessing
{
    public interface ITaskProcessor:IDisposable
    {
        internal void Suspend();
        internal void Resume();
        internal void Join();
        TaskProcessorState State { get;  }
        internal ParallelTaskProcessor Parent { get; }
        ITaskWorker Worker { get;  }
        internal int LowestPriority { get; }
        DateTime LastActivity { get; }
        internal void KillThread();
        internal void StartupThread();
        internal void Zombie();
    }

    public enum TaskProcessorState
    {
        Idle,
        Running,
        Stopping,
        Stopped,
        Zombie
    }
}
