using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.InMemory.Hub.ProtoExtensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Channels
{
    public class MemoryServiceChannel:IMemoryChannel
    {
        private readonly MscMode mode;
        private readonly int ttl;
        private readonly IIdentityProvider userProvider;
        private const string serverChannel = "{0}_srv";
        private const string clientChannel = "{0}_cli";

        private FilePipe incoming;
        private FilePipe outgoing;
        private CancellationTokenSource src;
        private CancellationToken endTok;
        private DateTime connectionLossTime;
        private bool connected;
        private ConcurrentDictionary<string, OperationWaitHandle> waitingOperations = new ConcurrentDictionary<string, OperationWaitHandle>();
        private Random reqRnd = new Random();
        private bool disposed = false;

        public MemoryServiceChannel(string name, bool biDirectional, MscMode mode, int ttl, IIdentityProvider userProvider)
        {
            Name = name;
            this.mode = mode;
            this.ttl = ttl;
            this.userProvider = userProvider;
            src = new CancellationTokenSource();
            endTok = src.Token;
            string incomingName = string.Empty, outgoingName = string .Empty;
            if (mode == MscMode.Client)
            {
                if (biDirectional)
                {
                    incomingName = string.Format(clientChannel, name);
                }

                outgoingName = string.Format(serverChannel, name);
            }
            else
            {
                if (biDirectional)
                {
                    outgoingName = string.Format(clientChannel, name);
                }

                incomingName = string.Format(serverChannel, name);
            }

            if (!string.IsNullOrEmpty(incomingName))
            {
                incoming = new FilePipe(incomingName);
                incoming.DataReceived += IncomingData;
                incoming.Listen();
            }

            if (!string.IsNullOrEmpty(outgoingName))
            {
                outgoing = new FilePipe(outgoingName);
            }

            connected = true;
            Connected = true;
        }

        public string Name { get; }

        public bool Connected { get; private set; }

        public int Ttl => ttl;

        public bool IsGlobal => incoming?.IsGlobal ?? outgoing?.IsGlobal ?? false;

        public void Dispose()
        {
            if (!disposed) {
                try
                {
                    var t = waitingOperations.Keys.ToArray();
                    foreach (var key in t)
                    {
                        var ok = waitingOperations.TryRemove(key, out var op);
                        if (!op.ServerResponse.Task.IsCompleted)
                        {
                            op.ServerResponse.SetException(new Exception("Service is shutting down."));
                        }
                    }

                    src?.Cancel();
                    src?.Dispose();
                    incoming?.Dispose();
                    outgoing?.Dispose();
                    src = null;
                    incoming = null;
                    outgoing = null;
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Error disconnecting: {ex.OutlineException()}", LogSeverity.Error);
                }
                finally
                {
                    disposed = true;
                }
            }
        }

        public CancellationToken CancellationToken => endTok;
        public async Task WriteAsync(object message)
        {
            if (outgoing != null)
            {
                try
                {
                    await outgoing.WriteAsync(JsonHelper.ToJsonStrongTyped(message), endTok);
                    connected = true;
                    if (!Connected)
                    {
                        Connected = true;
                        OnConnectionStatusChanged();
                    }
                }
                catch
                {
                    if (connected)
                    {
                        connected = false;
                        connectionLossTime = DateTime.Now;
                    }
                    else
                    {
                        var secs = DateTime.Now.Subtract(connectionLossTime).TotalSeconds;
                        if (secs > ttl && Connected)
                        {
                            Connected = false;
                            OnConnectionStatusChanged();
                        }
                    }

                    throw;
                }
            }
            else
            {
                throw new CommunicationException("This is a unidirectional channel!");
            }
        }

        public void Write(object message)
        {
            var awaiter = WriteAsync(message).ConfigureAwait(false).GetAwaiter();
            awaiter.GetResult();
        }

        public Task<object> Request(object requestMessage)
        {
            Hub.ProtoExtensions.Request r;
            lock (reqRnd)
            {
                r = new Request
                {
                    Payload = requestMessage,
                    RequestId = $"{mode}_{reqRnd.Next(25000)}_{DateTime.Now.Ticks}",
                    Identity = (userProvider?.CurrentIdentity != null) ? JsonHelper.ToJsonStrongTyped(userProvider.CurrentIdentity) : null
                };
            }

            return AsyncExtensions.CancelAfterAsync((c) =>
            {
                OperationWaitHandle wh = new OperationWaitHandle();
                waitingOperations.TryAdd(r.RequestId, wh);
                try
                {
                    Write(r);
                }
                catch (Exception ex)
                {
                    waitingOperations.TryRemove(r.RequestId, out _);
                    if (!wh.ServerResponse.Task.IsCompleted)
                    {
                        wh.ServerResponse.SetException(ex);
                    }
                }

                return wh.ServerResponse.Task;
            }, TimeSpan.FromSeconds(10), t =>
            {
                waitingOperations.TryRemove(r.RequestId, out _);
            });
        }

        protected virtual void OnConnectionStatusChanged()
        {
            ConnectionStatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnObjectReceived(ObjectReceivedEventArgs e)
        {
            ObjectReceived?.Invoke(this, e);
        }

        private void IncomingData(object? sender, IncomingDataEventArgs e)
        {
            Task.Run(async () =>
            {
                var context = new DataTransferContext();
                string requestId = null;
                var obj = JsonHelper.FromJsonStringStrongTyped<object>(e.Data);
                LogEnvironment.LogDebugEvent($"Message-Type: {obj.GetType()}", LogSeverity.Report);
                if (obj is Request req)
                {
                    obj = req.Payload;
                    requestId = req.RequestId;
                    if (!string.IsNullOrEmpty(req.Identity))
                    {
                        context.Identity = JsonHelper.FromJsonStringStrongTyped<TransferIdentity>(req.Identity).ToIdentity();
                    }
                }
                else if (obj is Response rep)
                {
                    obj = null;
                    var ok = waitingOperations.TryRemove(rep.RequestId, out var wait);
                    if (ok)
                    {
                        wait.ServerResponse.SetResult(rep.Payload);
                    }
                    else
                    {
                        throw new CommunicationException("The given operation is not open.");
                    }
                }

                if (obj != null)
                {
                    var evData = new ObjectReceivedEventArgs { Value = obj, Context = context };
                    OnObjectReceived(evData);
                    if (!string.IsNullOrEmpty(requestId) && outgoing != null)
                    {
                        await WriteAsync(new Response
                        {
                            RequestId = requestId,
                            Payload = evData.Result
                        });
                    }
                    else if (evData.Result != null)
                    {
                        await WriteAsync(evData.Result);
                    }
                }
            });
        }

        public event EventHandler ConnectionStatusChanged;

        public event EventHandler<ObjectReceivedEventArgs> ObjectReceived;
    }

    public enum MscMode
    {
        Server,
        Client
    }
}
