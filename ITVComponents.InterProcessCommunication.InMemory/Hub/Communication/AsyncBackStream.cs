using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Communication
{
    public class AsyncBackStream:IDisposable
    {
        private ConcurrentQueue<ServerOperationMessage> messageQueue;

        private TaskCompletionSource<ServerOperationMessage> openReadWait;

        private object messageSync = new object();

        private bool disposed;

        internal AsyncBackStream()
        {
            messageQueue = new ConcurrentQueue<ServerOperationMessage>();
        }

        public ServerOperationMessage Current { get; private set; }

        internal void PushMessage(ServerOperationMessage message)
        {
            TaskCompletionSource<ServerOperationMessage> target = null;
            lock (messageSync)
            {
                if (openReadWait != null)
                {
                    target = openReadWait;
                    openReadWait = null;
                }
                else
                {
                    messageQueue.Enqueue(message);
                }
            }

            if (target != null)
            {
                target.SetResult(message);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                lock (messageSync)
                {
                    if (openReadWait != null)
                    {
                        var t = openReadWait;
                        openReadWait = null;
                        t.SetCanceled();
                        //Monitor.Wait(messageSync);
                    }
                }

                while(messageQueue.TryDequeue(out var msg))
                {
                    LogEnvironment.LogDebugEvent($"{msg.OperationId} broken", LogSeverity.Warning);
                }
                disposed = true;
            }
        }

        public async Task<bool> MoveNext()
        {
            var t = Fetch();
            try
            {
                Current = await t.ConfigureAwait(false);
                if (Current == null)
                {
                    LogEnvironment.LogEvent("Current message was set to null!", LogSeverity.Warning);
                }

                return Current != null;
            }
            catch (TaskCanceledException)
            {
                LogEnvironment.LogEvent("Read cancelled!", LogSeverity.Warning);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogDebugEvent($"Un-Expected Error @MoveNext: {ex.OutlineException()}", LogSeverity.Error);
                throw;
            }

            return false;
        }

        private Task<ServerOperationMessage> Fetch()
        {
            Task<ServerOperationMessage> retVal;
            lock (messageSync)
            {
                if (messageQueue.IsEmpty && openReadWait == null)
                {
                    var ret = new TaskCompletionSource<ServerOperationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                    openReadWait = ret;
                    //Monitor.Pulse(messageSync);
                    retVal = openReadWait.Task;
                }
                else if (!messageQueue.IsEmpty && messageQueue.TryDequeue(out var item))
                {
                    retVal = Task.FromResult(item);
                }
                else
                {
                    LogEnvironment.LogDebugEvent("Peng!", LogSeverity.Warning);
                    throw new InvalidOperationException("Multiple reads not allowed!");
                }
            }

            return retVal;
        }
    }
}
