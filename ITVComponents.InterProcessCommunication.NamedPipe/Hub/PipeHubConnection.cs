using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.GenericService.ServiceSecurity;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.NamedPipe.Channels;
using ITVComponents.InterProcessCommunication.NamedPipe.Security;
using ITVComponents.Json;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Hub
{
    /// <summary>
    /// The hub's side of one client connection: passes the client's messages to the broker - under the identity
    /// the operating system reported for the pipe, never under one the client names.
    /// </summary>
    /// <remarks>
    /// What a pipe client may do:
    /// <list type="bullet">
    /// <item>call services and ask whether they exist - but not a client's back channel, unless it answers for a service
    /// this connection registered (so that a service connected through the pipe can raise its events);</item>
    /// <item>register a back channel for its events (a service named by a GUID that answers for another);
    /// a service of its own only if <see cref="NamedPipeHubOptions.AllowRemoteServices"/> allows it;</item>
    /// <item>tick, wait for requests and answer them - only for the services it registered itself.</item>
    /// </list>
    /// Whatever it registered is dropped when the connection ends. With an <see cref="NamedPipeHubOptions.Authorizer"/>,
    /// calling, looking up and registering a back channel need <see cref="PipeHubPermissions.ConnectAnyService"/> or a
    /// permission named like the service; registering a service needs <see cref="PipeHubPermissions.ActAsService"/>.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    internal sealed class PipeHubConnection
    {
        private readonly PipeMessageChannel channel;
        private readonly NamedPipeHubEndPoint endPoint;
        private readonly IServiceHubProvider hubProvider;
        private string transferIdentity;
        private readonly ClaimsIdentity callerIdentity;
        private readonly ConcurrentDictionary<string, bool> ownServices = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        private int ended;

        public PipeHubConnection(PipeMessageChannel channel, PipeClientIdentity client, NamedPipeHubEndPoint endPoint, IServiceHubProvider hubProvider)
        {
            this.channel = channel;
            this.endPoint = endPoint;
            this.hubProvider = hubProvider;
            Client = client;
            callerIdentity = client.ToClaimsIdentity();
            transferIdentity = TransferIdentityOf(client, Array.Empty<string>());
            channel.MessageReceived += MessageReceived;
            channel.Disconnected += ChannelDisconnected;
        }

        /// <summary>
        /// Gets who is at the other end.
        /// </summary>
        public PipeClientIdentity Client { get; }

        /// <summary>
        /// Is raised once, when the connection has ended and everything the client registered was dropped.
        /// </summary>
        public event EventHandler Ended;

        /// <summary>
        /// Starts serving the client.
        /// </summary>
        public void Start()
        {
            channel.Start();
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public void Close()
        {
            channel.Dispose();
        }

        private IEndPointBroker Broker => hubProvider.Broker ?? throw new CommunicationException("The hub is shutting down.");

        private void MessageReceived(object sender, PipeMessageReceivedEventArgs e)
        {
            switch (e.Value)
            {
                case ServerOperationMessage som:
                    e.Result = ConsumeService(som).ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
                case ServiceOperationResponseMessage sorm:
                    CommitServiceOperation(sorm);
                    break;
                case ServiceDiscoverMessage sdm:
                    e.Result = DiscoverService(sdm);
                    break;
                case RegisterServiceMessage rsm:
                    e.Result = RegisterService(rsm);
                    break;
                case ServiceSessionOperationMessage ssom when ssom.Tick:
                    e.Result = Tick(ssom);
                    break;
                case ServiceSessionOperationMessage ssom:
                    _ = ServiceReady(ssom);
                    break;
                default:
                    LogEnvironment.LogEvent($"Unexpected {e.Value.GetType().Name} from pipe client {Client}; it is ignored.", LogSeverity.Warning);
                    break;
            }
        }

        private async Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request)
        {
            try
            {
                if (!MayReach(request.TargetService, out var reason))
                {
                    throw new SecurityException(reason);
                }

                // The identity of the caller is what the pipe says - whatever the client put there is replaced.
                request.HubUser = transferIdentity;
                request.TickBack = false;
                return await Broker.SendMessageToServer(request, null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Pipe client {Client} could not call {request.TargetService}: {ex.Message}", ex is SecurityException ? LogSeverity.Warning : LogSeverity.Error);
                return new ServiceOperationResponseMessage
                {
                    Ok = false,
                    OperationId = request.OperationId,
                    TargetService = request.TargetService,
                    ResponsePayload = JsonHelper.ToJson(new ErrorResponse { SerializedException = ex }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true)
                };
            }
        }

        private void CommitServiceOperation(ServiceOperationResponseMessage response)
        {
            if (!ownServices.ContainsKey(response.TargetService ?? string.Empty))
            {
                LogEnvironment.LogEvent($"Pipe client {Client} answered for {response.TargetService}, which it did not register; the answer is dropped.", LogSeverity.Warning);
                return;
            }

            Broker.CommitServerOperation(response);
        }

        private ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage request)
        {
            if (!MayReach(request.TargetService, out var reason))
            {
                LogEnvironment.LogDebugEvent($"Pipe client {Client} looked up {request.TargetService}: {reason}", LogSeverity.Report);
                return new ServiceDiscoverResponseMessage { TargetService = request.TargetService, Ok = false, Reason = "Service is not available" };
            }

            return Broker.DiscoverService(request);
        }

        private RegisterServiceResponseMessage RegisterService(RegisterServiceMessage request)
        {
            var isBackChannel = !string.IsNullOrEmpty(request.ResponderFor);
            string refusal = null;
            if (string.IsNullOrEmpty(request.ServiceName) || request.ServiceName.Contains('@'))
            {
                refusal = "A pipe client cannot register a service without name or on another hub.";
            }
            else if (isBackChannel && !Guid.TryParse(request.ServiceName, out _))
            {
                // A back channel named by a GUID can never take the name of a real service, not even of one that
                // has not registered yet.
                refusal = "The back channel of a pipe client must be named by a GUID.";
            }
            else if (!isBackChannel && !endPoint.Options.AllowRemoteServices)
            {
                refusal = "This hub does not accept services from pipe clients.";
            }
            else if (isBackChannel && !Authorized(PipeHubPermissions.ConnectAnyService, request.ResponderFor))
            {
                refusal = $"The caller may not use {request.ResponderFor}.";
            }
            else if (!isBackChannel && !Authorized(PipeHubPermissions.ActAsService))
            {
                refusal = "The caller may not act as a service.";
            }

            if (refusal != null)
            {
                LogEnvironment.LogEvent($"Pipe client {Client} wanted to register {request.ServiceName}: refused. {refusal}", LogSeverity.Warning);
                return new RegisterServiceResponseMessage { Ok = false, Reason = refusal };
            }

            var result = Broker.RegisterService(request, null);
            if (result.Ok)
            {
                ownServices[request.ServiceName] = isBackChannel;

                // The same bookkeeping as the other authenticated hubs: a service is run by this caller, and a back channel
                // may be called by whoever runs the service it answers for - over whichever transport that service came.
                if (isBackChannel)
                {
                    TemporaryGrants.GrantTemporaryPermission(request.ResponderFor, request.ServiceName);
                }
                else
                {
                    TemporaryGrants.RegisterService(request.ServiceName, Client.UserName);
                }

                if (isBackChannel)
                {
                    endPoint.AddBackChannel(request.ServiceName, request.ResponderFor);
                    transferIdentity = TransferIdentityOf(Client, ownServices.Where(n => n.Value).Select(n => n.Key));
                }

                LogEnvironment.LogDebugEvent($"Pipe client {Client} registered {(isBackChannel ? "the back channel" : "the service")} {request.ServiceName}.", LogSeverity.Report);
            }
            else
            {
                LogEnvironment.LogEvent($"Pipe client {Client} could not register {request.ServiceName}: {result.Reason}", LogSeverity.Warning);
            }

            return result;
        }

        private ServiceTickResponseMessage Tick(ServiceSessionOperationMessage request)
        {
            if (!ownServices.ContainsKey(request.ServiceName ?? string.Empty))
            {
                LogEnvironment.LogEvent($"Pipe client {Client} ticked for {request.ServiceName}, which it did not register.", LogSeverity.Warning);
                return new ServiceTickResponseMessage { Ok = false, Reason = "The service was not registered over this connection." };
            }

            return Broker.Tick(request);
        }

        private async Task ServiceReady(ServiceSessionOperationMessage request)
        {
            if (!ownServices.ContainsKey(request.ServiceName ?? string.Empty))
            {
                LogEnvironment.LogEvent($"Pipe client {Client} wanted to serve {request.ServiceName}, which it did not register; refused.", LogSeverity.Warning);
                return;
            }

            var closing = channel.Closing;
            try
            {
                while (!closing.IsCancellationRequested)
                {
                    string operationId = null;
                    try
                    {
                        var next = await Broker.NextRequest(request).ConfigureAwait(false);
                        if (next != null)
                        {
                            operationId = next.OperationId;
                            await channel.WriteAsync(new ServerOperationMessage
                            {
                                HubUser = next.HubUser,
                                OperationId = next.OperationId,
                                OperationPayload = next.OperationPayload,
                                TargetService = next.TargetService,
                                TickBack = next.TickBack
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            await Task.Delay(500, closing).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex) when (ex is ServiceBrokenException || ex is ServiceTimeoutException || ex is ServiceUnknownException || ex is CommunicationException)
                    {
                        TryFail(request, operationId, ex);
                        if (closing.IsCancellationRequested)
                        {
                            break;
                        }

                        LogEnvironment.LogEvent($"Serving {request.ServiceName} for pipe client {Client} ends: {ex.Message}", LogSeverity.Warning);
                        break;
                    }
                    catch (Exception ex)
                    {
                        TryFail(request, operationId, ex);
                        LogEnvironment.LogEvent($"Serving {request.ServiceName} for pipe client {Client} failed: {ex.OutlineException()}", LogSeverity.Error);
                    }
                }
            }
            finally
            {
                if (hubProvider.Broker != null && !hubProvider.Broker.TryUnRegisterService(request))
                {
                    LogEnvironment.LogDebugEvent($"{request.ServiceName} of pipe client {Client} was already unregistered.", LogSeverity.Report);
                }
            }
        }

        /// <summary>
        /// Decides whether this connection may call or look up a service.
        /// </summary>
        private bool MayReach(string targetService, out string reason)
        {
            reason = null;
            if (endPoint.IsBackChannelOfAPipeClient(targetService, out var answersFor))
            {
                // A back channel is reached only by the service it answers for - here: a service registered over this
                // very connection, raising its events.
                if (ownServices.TryGetValue(answersFor ?? string.Empty, out var ownIsBackChannel) && !ownIsBackChannel)
                {
                    return true;
                }

                reason = "The back channel of a pipe client can only be called by the service it answers for.";
                return false;
            }

            if (!Authorized(PipeHubPermissions.ConnectAnyService, targetService))
            {
                reason = $"The caller may not use {targetService}.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Asks the hub's authorization, if there is one.
        /// </summary>
        private bool Authorized(params string[] permissions)
        {
            var authorizer = endPoint.Options.Authorizer;
            if (authorizer == null)
            {
                return true;
            }

            try
            {
                var granted = authorizer.HasAnyPermission(callerIdentity, permissions.Where(p => !string.IsNullOrEmpty(p)).ToArray());
                if (!granted)
                {
                    LogEnvironment.LogEvent($"Pipe client {Client} has none of the permissions {string.Join(", ", permissions)}.", LogSeverity.Warning);
                }

                return granted;
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"The permissions of pipe client {Client} could not be checked; the access is refused: {ex.OutlineException()}", LogSeverity.Error);
                return false;
            }
        }

        private static string TransferIdentityOf(PipeClientIdentity client, IEnumerable<string> backChannels)
        {
            return JsonHelper.ToJson(client.ToClaimsIdentity(backChannels).ForTransfer(), SerializationTypingMode.StaticTyping, null);
        }

        private void TryFail(ServiceSessionOperationMessage session, string operationId, Exception reason)
        {
            try
            {
                hubProvider.Broker?.FailOperation(session, operationId, reason);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Operation {operationId} of {session.ServiceName} could not be failed: {ex.OutlineException()}", LogSeverity.Warning);
            }
        }

        private void ChannelDisconnected(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref ended, 1) != 0)
            {
                return;
            }

            channel.MessageReceived -= MessageReceived;
            channel.Disconnected -= ChannelDisconnected;
            foreach (var own in ownServices)
            {
                var service = own.Key;
                if (own.Value)
                {
                    if (endPoint.IsBackChannelOfAPipeClient(service, out var answersFor) && !string.IsNullOrEmpty(answersFor))
                    {
                        TemporaryGrants.RevokeTemporaryPermission(answersFor, service);
                    }
                }
                else
                {
                    TemporaryGrants.UnRegisterService(service);
                }

                endPoint.RemoveBackChannel(service);
                try
                {
                    hubProvider.Broker?.UnsafeServerDrop(service);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"{service} of pipe client {Client} could not be dropped: {ex.OutlineException()}", LogSeverity.Error);
                }
            }

            ownServices.Clear();
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }
}
