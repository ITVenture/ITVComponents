using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Json;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Channels
{
    /// <summary>
    /// Exchanges protocol messages over one pipe connection, in both directions: a frame is a 4-byte length
    /// (little endian) followed by the message as UTF-8 JSON.
    /// </summary>
    /// <remarks>
    /// Each incoming message is handled on its own task, so a long-running handler (a service waiting for
    /// requests) does not block the connection. A frame larger than the configured maximum ends the connection:
    /// the other side either does not speak this protocol or tries to exhaust the memory.
    /// </remarks>
    internal sealed class PipeMessageChannel : IDisposable
    {
        private readonly PipeStream stream;
        private readonly int maxMessageSize;
        private readonly string name;
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IProtocolMessage>> openRequests = new ConcurrentDictionary<string, TaskCompletionSource<IProtocolMessage>>();
        private long nextRequest;
        private int closed;

        static PipeMessageChannel()
        {
            JsonHelper.ExtendNativeProtocolType<IProtocolMessage, PipeRequest>("PipeRequest");
            JsonHelper.ExtendNativeProtocolType<IProtocolMessage, PipeResponse>("PipeResponse");
            JsonHelper.ExtendNativeProtocolType<IProtocolMessage, PipeDisconnect>("PipeDisconnect");

            // The hub answers these as the payload of a PipeResponse, typed as IProtocolMessage - but the shared
            // protocol does not list them as its derived types, so they would not serialize there.
            JsonHelper.ExtendNativeProtocolType<IProtocolMessage, RegisterServiceResponseMessage>("PipeRegisterServiceResponse");
            JsonHelper.ExtendNativeProtocolType<IProtocolMessage, ServiceTickResponseMessage>("PipeServiceTickResponse");
        }

        /// <summary>
        /// Initializes a new channel over a connected pipe. Call <see cref="Start"/> to begin reading.
        /// </summary>
        /// <param name="stream">the connected pipe; the channel owns it from now on</param>
        /// <param name="maxMessageSize">the largest frame accepted, in bytes</param>
        /// <param name="name">a name for the log</param>
        public PipeMessageChannel(PipeStream stream, int maxMessageSize, string name)
        {
            this.stream = stream;
            this.maxMessageSize = maxMessageSize;
            this.name = name;
        }

        /// <summary>
        /// Gets a value indicating whether the connection is still open.
        /// </summary>
        public bool Connected => Volatile.Read(ref closed) == 0;

        /// <summary>
        /// Gets a token that is cancelled when the connection closes.
        /// </summary>
        public CancellationToken Closing => lifetime.Token;

        /// <summary>
        /// Is raised for every message that is not the answer to an own request. For a <see cref="PipeRequest"/>,
        /// the handler puts the answer into <see cref="PipeMessageReceivedEventArgs.Result"/>.
        /// </summary>
        public event EventHandler<PipeMessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Is raised once, when the connection has closed.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Starts reading from the pipe.
        /// </summary>
        public void Start()
        {
            Task.Run(ReadLoop);
        }

        /// <summary>
        /// Sends a message without expecting an answer.
        /// </summary>
        public async Task WriteAsync(IProtocolMessage message)
        {
            if (!Connected)
            {
                throw new CommunicationException($"The pipe connection {name} is closed.");
            }

            var payload = Encoding.UTF8.GetBytes(JsonHelper.ToJson(message, SerializationTypingMode.NativePolymorphism, typeof(IProtocolMessage), true));
            if (payload.Length > maxMessageSize)
            {
                throw new CommunicationException($"The message ({payload.Length} bytes) exceeds the maximum of {maxMessageSize} bytes of the pipe connection {name}.");
            }

            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
            try
            {
                await writeLock.WaitAsync(lifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new CommunicationException($"The pipe connection {name} is closed.");
            }

            try
            {
                await stream.WriteAsync(header, lifetime.Token).ConfigureAwait(false);
                await stream.WriteAsync(payload, lifetime.Token).ConfigureAwait(false);
                await stream.FlushAsync(lifetime.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException)
            {
                Close($"writing failed: {ex.Message}");
                throw new CommunicationException($"The pipe connection {name} is closed ({ex.Message}).");
            }
            finally
            {
                writeLock.Release();
            }
        }

        /// <summary>
        /// Sends a message and waits for the answer.
        /// </summary>
        /// <param name="message">the message</param>
        /// <param name="timeout">how long to wait for the answer</param>
        /// <returns>the answer</returns>
        public async Task<IProtocolMessage> RequestAsync(IProtocolMessage message, TimeSpan timeout)
        {
            var requestId = Interlocked.Increment(ref nextRequest).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var answer = new TaskCompletionSource<IProtocolMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            openRequests[requestId] = answer;
            try
            {
                await WriteAsync(new PipeRequest { RequestId = requestId, Payload = message }).ConfigureAwait(false);
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeoutSource.CancelAfter(timeout);
                using (timeoutSource.Token.Register(() => answer.TrySetException(Connected
                           ? new TimeoutException($"No answer on the pipe connection {name} within {timeout}.")
                           : new CommunicationException($"The pipe connection {name} closed before the answer arrived."))))
                {
                    return await answer.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                openRequests.TryRemove(requestId, out _);
            }
        }

        /// <summary>
        /// Announces the end of the connection to the other side and closes it.
        /// </summary>
        public void Dispose()
        {
            if (Connected)
            {
                try
                {
                    WriteAsync(new PipeDisconnect()).Wait(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Announcing the end of pipe connection {name} failed: {ex.Message}", LogSeverity.Report);
                }
            }

            Close("disposed");
        }

        private async Task ReadLoop()
        {
            var header = new byte[4];
            try
            {
                while (!lifetime.IsCancellationRequested)
                {
                    if (!await ReadExactly(header).ConfigureAwait(false))
                    {
                        Close("the other side closed the pipe");
                        return;
                    }

                    var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (length <= 0 || length > maxMessageSize)
                    {
                        LogEnvironment.LogEvent($"Pipe connection {name} announced a message of {length} bytes (maximum {maxMessageSize}); the connection is closed.", LogSeverity.Warning);
                        Close("invalid frame length");
                        return;
                    }

                    var payload = new byte[length];
                    if (!await ReadExactly(payload).ConfigureAwait(false))
                    {
                        Close("the other side closed the pipe within a message");
                        return;
                    }

                    var json = Encoding.UTF8.GetString(payload);
                    _ = Task.Run(() => Dispatch(json));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException)
            {
                Close($"reading failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Unexpected error reading pipe connection {name}: {ex.OutlineException()}", LogSeverity.Error);
                Close("unexpected read error");
            }
        }

        private async Task<bool> ReadExactly(byte[] buffer)
        {
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(read), lifetime.Token).ConfigureAwait(false);
                if (n == 0)
                {
                    return false;
                }

                read += n;
            }

            return true;
        }

        private async Task Dispatch(string json)
        {
            IProtocolMessage message;
            try
            {
                message = JsonHelper.FromJsonString<IProtocolMessage>(json, SerializationTypingMode.NativePolymorphism, true);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Pipe connection {name} received a message that could not be read; the connection is closed: {ex.OutlineException()}", LogSeverity.Warning);
                Close("unreadable message");
                return;
            }

            if (message is PipeResponse response)
            {
                if (openRequests.TryRemove(response.RequestId ?? string.Empty, out var waiting))
                {
                    if (response.Error != null)
                    {
                        waiting.TrySetException(new CommunicationException(response.Error));
                    }
                    else
                    {
                        waiting.TrySetResult(response.Payload);
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"Pipe connection {name} received an answer to request {response.RequestId}, which is not open (timed out?).", LogSeverity.Warning);
                }

                return;
            }

            if (message is PipeDisconnect)
            {
                Close("the other side announced the end");
                return;
            }

            var request = message as PipeRequest;
            var args = new PipeMessageReceivedEventArgs(request?.Payload ?? message);
            string error = null;
            try
            {
                if (args.Value == null)
                {
                    throw new InvalidOperationException("The request carries no message.");
                }

                MessageReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Processing a {args.Value?.GetType().Name ?? "empty"} message on pipe connection {name} failed: {ex.OutlineException()}", LogSeverity.Error);
                error = ex.Message;
            }

            if (request != null && Connected)
            {
                try
                {
                    await WriteAsync(new PipeResponse { RequestId = request.RequestId, Payload = error == null ? args.Result : null, Error = error }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"The answer to request {request.RequestId} could not be sent on pipe connection {name}: {ex.OutlineException()}", LogSeverity.Error);
                }
            }
        }

        private void Close(string reason)
        {
            if (Interlocked.Exchange(ref closed, 1) != 0)
            {
                return;
            }

            LogEnvironment.LogDebugEvent($"Pipe connection {name} closes: {reason}.", LogSeverity.Report);
            try
            {
                lifetime.Cancel();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Cancelling the work of pipe connection {name} failed: {ex.OutlineException()}", LogSeverity.Warning);
            }

            foreach (var key in openRequests.Keys)
            {
                if (openRequests.TryRemove(key, out var waiting))
                {
                    waiting.TrySetException(new CommunicationException($"The pipe connection {name} closed before the answer arrived."));
                }
            }

            try
            {
                stream.Dispose();
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Closing the pipe of connection {name} failed: {ex.OutlineException()}", LogSeverity.Warning);
            }

            try
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"A listener on the end of pipe connection {name} failed: {ex.OutlineException()}", LogSeverity.Error);
            }
        }
    }

    /// <summary>
    /// A message that arrived on a pipe connection.
    /// </summary>
    internal sealed class PipeMessageReceivedEventArgs : EventArgs
    {
        public PipeMessageReceivedEventArgs(IProtocolMessage value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public IProtocolMessage Value { get; }

        /// <summary>
        /// Gets or sets the answer, if the message was a request.
        /// </summary>
        public IProtocolMessage Result { get; set; }
    }
}
