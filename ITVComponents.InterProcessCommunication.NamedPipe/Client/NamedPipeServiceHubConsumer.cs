using System;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.NamedPipe.Channels;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Json;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Client
{
    /// <summary>
    /// A connection to a hub over a named pipe - for a client, and for a service that runs outside the hub's process.
    /// </summary>
    /// <remarks>
    /// The client connects anonymously and always under the account of its process: the hub cannot act as it, and it
    /// sends no identity of its own either - the hub reads it from the operating system.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class NamedPipeServiceHubConsumer : IHubConnection
    {
        private const int TickPeriod = 6000;
        private const int ServiceTtl = 15;

        private readonly NamedPipeHubClientOptions options;
        private readonly string myServiceName;
        private readonly string consumedService;
        private readonly ICustomServerSecurity customServerSecurity;
        private readonly Timer tickTimer;
        private readonly object initLock = new object();
        private PipeMessageChannel channel;
        private ServiceSessionOperationMessage session;
        private long nextOperation;
        private bool initialized;
        private bool disposing;

        /// <summary>
        /// Initializes a new connection.
        /// </summary>
        /// <param name="options">how to reach the hub</param>
        /// <param name="serviceName">the name this connection registers at the hub: the back channel of a client (a GUID), the name of a service, or null to only call</param>
        /// <param name="consumedService">for a back channel: the service it answers for</param>
        /// <param name="customServerSecurity">for a service: transforms the identity of its callers</param>
        public NamedPipeServiceHubConsumer(NamedPipeHubClientOptions options, string serviceName, string consumedService, ICustomServerSecurity customServerSecurity)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            myServiceName = serviceName;
            this.consumedService = consumedService;
            this.customServerSecurity = customServerSecurity;
            tickTimer = new Timer(SendTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <inheritdoc />
        public string ServiceName { get; private set; }

        /// <inheritdoc />
        public bool Operational { get; private set; }

        /// <inheritdoc />
        bool IHubConnection.Initialized => initialized;

        /// <inheritdoc />
        public event EventHandler<MessageArrivedEventArgs> MessageArrived;

        /// <inheritdoc />
        public event EventHandler OperationalChanged;

        /// <inheritdoc />
        void IHubConnection.Initialize()
        {
            lock (initLock)
            {
                if (initialized || disposing)
                {
                    return;
                }

                try
                {
                    Connect();
                    initialized = true;
                    SetOperational(true);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Connecting to the hub pipe {options.PipeName} failed: {ex.OutlineException()}", LogSeverity.Error);
                    CloseChannel();
                }
            }
        }

        /// <inheritdoc />
        public string InvokeService(string serviceName, string serviceMessage)
        {
            return InvokeServiceAsync(serviceName, serviceMessage).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public async Task<string> InvokeServiceAsync(string serviceName, string serviceMessage)
        {
            var response = await Channel.RequestAsync(new ServerOperationMessage
            {
                OperationId = $"{serviceName}_{Interlocked.Increment(ref nextOperation)}_{DateTime.UtcNow.Ticks}",
                OperationPayload = serviceMessage,
                TargetService = serviceName,
                TickBack = false
            }, options.CallTimeout).ConfigureAwait(false);
            return ((ServiceOperationResponseMessage)response).ResponsePayload;
        }

        /// <inheritdoc />
        public bool DiscoverService(string serviceName)
        {
            try
            {
                var response = (ServiceDiscoverResponseMessage)Channel.RequestAsync(new ServiceDiscoverMessage { TargetService = serviceName }, options.RequestTimeout)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (!response.Ok)
                {
                    LogEnvironment.LogEvent($"Service {serviceName} is not available on hub pipe {options.PipeName}: {response.Reason}", LogSeverity.Warning);
                }

                return response.Ok;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Discovering {serviceName} on hub pipe {options.PipeName} failed: {ex.Message}", LogSeverity.Warning);
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposing)
            {
                return;
            }

            disposing = true;
            tickTimer.Dispose();
            CloseChannel();
        }

        private PipeMessageChannel Channel => channel is { Connected: true } c
            ? c
            : throw new CommunicationException($"The connection to the hub pipe {options.PipeName} is closed.");

        private void Connect()
        {
            var stream = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous);
            try
            {
                // Always under the process' own account, even if this thread impersonates someone (a web application
                // acting for its user): the pipe's ACL checks the token that opens it, and the hub identifies the caller
                // by its process - both must see the same account.
                WindowsIdentity.RunImpersonated(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle.InvalidHandle,
                    () => stream.Connect((int)options.ConnectTimeout.TotalMilliseconds));
                VerifyOwner(stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            channel = new PipeMessageChannel(stream, options.MaxMessageSize, $"{options.PipeName} (client)");
            channel.MessageReceived += MessageFromHub;
            channel.Disconnected += ChannelDisconnected;
            channel.Start();
            ServiceName = myServiceName;
            if (string.IsNullOrEmpty(ServiceName))
            {
                return;
            }

            var registration = new RegisterServiceMessage { ServiceName = ServiceName, Ttl = ServiceTtl, ResponderFor = string.IsNullOrEmpty(consumedService) ? null : consumedService };
            var result = (RegisterServiceResponseMessage)channel.RequestAsync(registration, options.RequestTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
            if (!result.Ok)
            {
                ServiceName = null;
                throw new CommunicationException($"Unable to register {registration.ServiceName} at the hub: {result.Reason}");
            }

            session = new ServiceSessionOperationMessage
            {
                ServiceName = registration.ServiceName,
                SessionTicket = result.SessionTicket,
                Ttl = registration.Ttl,
                ResponderFor = registration.ResponderFor
            };
            channel.WriteAsync(session).ConfigureAwait(false).GetAwaiter().GetResult();
            tickTimer.Change(TickPeriod, TickPeriod);
        }

        private void VerifyOwner(NamedPipeClientStream stream)
        {
            if (options.ExpectedServerOwner == null)
            {
                return;
            }

            var owner = stream.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner != options.ExpectedServerOwner)
            {
                throw new SecurityException($"The pipe {options.PipeName} is owned by {owner?.Value ?? "nobody"}, not by {options.ExpectedServerOwner.Value}: it was not created by the hub.");
            }
        }

        private void MessageFromHub(object sender, PipeMessageReceivedEventArgs e)
        {
            if (e.Value is not ServerOperationMessage request)
            {
                LogEnvironment.LogEvent($"Unexpected {e.Value.GetType().Name} from hub pipe {options.PipeName}; it is ignored.", LogSeverity.Warning);
                return;
            }

            if (request.TickBack)
            {
                return;
            }

            var arrived = new MessageArrivedEventArgs
            {
                Message = request.OperationPayload,
                TargetService = request.TargetService
            };
            if (!string.IsNullOrEmpty(request.HubUser))
            {
                arrived.HubUser = JsonHelper.FromJsonString<TransferIdentity>(request.HubUser, SerializationTypingMode.StaticTyping).ToIdentity(customServerSecurity);
            }

            MessageArrived?.Invoke(this, arrived);
            var response = new ServiceOperationResponseMessage
            {
                OperationId = request.OperationId,
                TargetService = request.TargetService,
                ResponderFor = string.IsNullOrEmpty(consumedService) ? null : consumedService
            };
            if (!arrived.Completed)
            {
                response.Ok = false;
                response.ResponsePayload = JsonHelper.ToJson(new ErrorResponse { SerializedException = new SerializedException("Message was not processed!", Array.Empty<SerializedException>()) }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
            }
            else
            {
                response.Ok = arrived.Error == null;
                response.ResponsePayload = arrived.Error ?? arrived.Response;
            }

            channel.WriteAsync(response).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void SendTick(object state)
        {
            var current = session;
            if (current == null || channel is not { Connected: true })
            {
                return;
            }

            try
            {
                var answer = (ServiceTickResponseMessage)channel.RequestAsync(new ServiceSessionOperationMessage
                {
                    ResponderFor = current.ResponderFor,
                    ServiceName = current.ServiceName,
                    SessionTicket = current.SessionTicket,
                    Tick = true,
                    Ttl = current.Ttl
                }, options.RequestTimeout).ConfigureAwait(false).GetAwaiter().GetResult();
                if (!answer.Ok)
                {
                    LogEnvironment.LogEvent($"The hub refused the tick of {current.ServiceName}: {answer.Reason}", LogSeverity.Warning);
                }
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Ticking {current.ServiceName} at hub pipe {options.PipeName} failed: {ex.Message}", LogSeverity.Warning);
            }
        }

        private void ChannelDisconnected(object sender, EventArgs e)
        {
            tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (!disposing)
            {
                LogEnvironment.LogEvent($"The connection to the hub pipe {options.PipeName} was lost.", LogSeverity.Warning);
                initialized = false;
                SetOperational(false);
            }
        }

        private void CloseChannel()
        {
            var current = channel;
            channel = null;
            if (current == null)
            {
                return;
            }

            current.MessageReceived -= MessageFromHub;
            current.Disconnected -= ChannelDisconnected;
            current.Dispose();
        }

        private void SetOperational(bool value)
        {
            if (disposing)
            {
                return;
            }

            Operational = value;
            OperationalChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
