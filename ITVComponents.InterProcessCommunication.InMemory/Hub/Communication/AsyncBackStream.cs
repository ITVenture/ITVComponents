using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            lock (messageSync)
            {
                if (openReadWait != null)
                {
                    var t = openReadWait;
                    openReadWait = null;
                    t.SetResult(message);
                }
                else
                {
                    messageQueue.Enqueue(message);
                }
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
                Current = await t;
                return Current != null;
            }
            catch (TaskCanceledException)
            {

            }

            return false;
        }

        private Task<ServerOperationMessage> Fetch()
        {
            lock (messageSync)
            {
                if (messageQueue.IsEmpty && openReadWait == null)
                {
                    openReadWait = new TaskCompletionSource<ServerOperationMessage>(TaskCreationOptions.None);
                    //Monitor.Pulse(messageSync);
                    return openReadWait.Task;
                }
                else if (!messageQueue.IsEmpty && messageQueue.TryDequeue(out var item))
                {
                    return Task.FromResult(item);
                }

                LogEnvironment.LogDebugEvent("Peng!", LogSeverity.Warning);
                throw new InvalidOperationException("Multiple reads not allowed!");
            }
        }
    }
}
