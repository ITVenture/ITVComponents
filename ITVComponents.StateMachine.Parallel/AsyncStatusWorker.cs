using System.Collections.Concurrent;
using ITVComponents.ParallelProcessing;
using ITVComponents.ParallelProcessing.Helpers;
using ITVComponents.Threading;

namespace ITVComponents.StateMachine.Parallel
{
    public class AsyncStatusWorker: IStatusWorker, IDisposable
    {
        private readonly ManualResetEvent stopEvent;
        private readonly AsyncStatusProcessor parent;
        private ManualResetEvent stopped;
        private readonly List<ConcurrentQueue<ITask>> workingQueue;
        private bool initialized = false;

        public AsyncStatusWorker(ManualResetEvent stopEvent, AsyncStatusProcessor parent, List<ConcurrentQueue<ITask>> workingQueue)
        {
            this.stopEvent = stopEvent;
            this.parent = parent;
            this.workingQueue = workingQueue;
        }

        public void Initialize()
        {
            if (!initialized)
            {
                try
                {
                    stopped = new ManualResetEvent(false);
                    ThreadPool.RegisterWaitForSingleObject(stopEvent, Work, null, 100, false);
                }
                finally
                {
                    initialized = true;
                }
            }
        }

        private void Work(object? state, bool timedOut)
        {
            if (timedOut)
            {
                try
                {
                    AsyncHelpers.RunSync(async () =>
                    {
                        var tasks = workingQueue.Select(n => WorkQueue(n)).ToArray();
                        await Task.WhenAll(tasks);
                    });
                }
                catch (Exception ex)
                {
                    parent.HandleWorkerException(ex);
                }
            }
            else
            {
                stopped.Set();
            }
        }

        private async Task WorkQueue(ConcurrentQueue<ITask> sourceQueue)
        {
            if (sourceQueue.TryDequeue(out var task))
            {
                try
                {
                    await task.Processing();
                    if (task.Done && !task.Wait)
                    {
                        sourceQueue.Enqueue(task);
                    }
                }
                catch (Exception ex)
                {
                    parent.HandleWorkerException(ex, task);
                }
            }
        }

        public void Dispose()
        {
            stopped.WaitOne();
        }
    }
}
