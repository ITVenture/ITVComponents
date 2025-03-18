using System;
using System.Linq;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages.ProtocolHelper;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Json;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;
using ITVComponents.Threading;
using Microsoft.AspNetCore.StaticFiles;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Client
{
    public class MessageClient:BaseClient
    {
        private bool isBidirectional;

        private readonly IIdentityProvider identityProvider;
        private IHubConnection connection;
        private readonly string targetService;
        private readonly bool useEvents;
        private readonly IHubConnectionFactory connector;
        private bool connected;
        private bool initCalled;

        public MessageClient(IServiceHubProvider serviceHub, string targetService, bool useEvents):base($"local/{targetService}")
        {
            var tailId = targetService.IndexOf("@");
            string tail = null;
            if (tailId != -1)
            {
                tail = targetService.Substring(tailId);
            }
            if (!useEvents)
            {
                connection = new LocalServiceHubConsumer(null, serviceHub, targetService, null);
            }
            else
            {
                connection = new LocalServiceHubConsumer($"{Guid.NewGuid()}{tail}", serviceHub, targetService, null);
                isBidirectional = true;
            }

            this.targetService = targetService;
            this.useEvents = useEvents;
            if (useEvents)
            {
                connection.MessageArrived += ProcessMessage;
            }

            connected = true;
        }

        public MessageClient(IServiceHubProvider serviceHub, string targetService, IIdentityProvider identityProvider, bool useEvents):this(serviceHub,targetService,useEvents)
        {
            this.identityProvider = identityProvider;
        }

        protected MessageClient(IHubConnectionFactory connector, string targetService, IIdentityProvider identityProvider, bool  useEvents):base($"{connector.Target}/{targetService}")
        {
            this.connector = connector;
            this.targetService = targetService;
            isBidirectional = useEvents;
            this.useEvents = useEvents;
            this.identityProvider = identityProvider;
            ReConnectClient();
        }

        static MessageClient()
        {
            MessageTranslator.RegisterMessages();
        }

        public override bool IsBidirectional => isBidirectional && !string.IsNullOrEmpty(connection.ServiceName);

        /// <summary>
        /// Implements a method to check on the remote object whether a specific object is available
        /// </summary>
        /// <param name="uniqueName">the unique name of the desired object</param>
        /// <returns>an object that provides information about the proxy-availability</returns>
        protected override ObjectAvailabilityResult CheckForAvailableProxy(string uniqueName)
        {
            if (ValidateConnection())
            {
                var msg = new ObjectAvailabilityRequestMessage
                {
                    UniqueName = uniqueName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                };

                var msgString = JsonHelper.ToJson(msg, SerializationTypingMode.NativePolymorphism,
                    typeof(IRequestMessage), true);
                var tk = connection.InvokeServiceAsync(targetService, msgString);
                var response = tk.TestServerMessage<ObjectAvailabilityResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter()
                    .GetResult();
                return new ObjectAvailabilityResult {Available = response.Available, Message = response.Message};
            }

            return new ObjectAvailabilityResult
            {
                Available = false,
                Message = "Service is unavailable!"
            };
        }

        /// <summary>
        /// Implements a method to abandon an extended proxy-object that was created by a separate client-call before
        /// </summary>
        /// <param name="uniqueName">the unique name of the proxy that needs to be released</param>
        /// <returns>a value indicating whether the release of the object was successful</returns>
        protected override bool AbandonExtendedProxy(string uniqueName)
        {
            if (connected)
            {
                var msgStr = JsonHelper.ToJson(new AbandonExtendedProxyRequestMessage
                {
                    ObjectName = uniqueName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true);
                var tk = connection.InvokeServiceAsync(targetService, msgStr);
                return tk.TestServerMessage<AbandonExtendedProxyResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult().Result;
            }

            throw new InterProcessException("Not connected!", null);
        }

        protected override object GetPropertyValue(string uniqueName, string propertyName, object[] index)
        {
            if (connected)
            {
                var msgStr = JsonHelper.ToJson(new GetPropertyRequestMessage
                {
                    TargetMethod = propertyName,
                    TargetObject = uniqueName,
                    MethodArguments = index.PackArguments(),
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true);
                var tk = connection.InvokeServiceAsync(targetService, msgStr);
                return tk.TestServerMessage<GetPropertyResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult().Result;
            }

            throw new InterProcessException("Not connected!", null);
        }

        protected override void SetPropertyValue(string uniqueName, string propertyName, object[] index, object value)
        {
            if (connected)
            {
                var msgStr = JsonHelper.ToJson(new SetPropertyRequestMessage
                {
                    TargetMethod = propertyName,
                    TargetObject = uniqueName,
                    MethodArguments = index.PackArguments(),
                    Value = value,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true);
                var tk = connection.InvokeServiceAsync(targetService, msgStr);
                var tmp = tk.TestServerMessage<SetPropertyResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult();
                if (!tmp.Ok)
                {
                    throw new InterProcessException("Set-Property was not successful", null);
                }
            }
            else
            {
                throw new InterProcessException("Not connected!", null);
            }
        }

        /// <summary>
        /// Implements a method to request the execution of the demanded method with the given arguments on the remote process
        /// </summary>
        /// <param name="uniqueName">the name of the object on which the method must be executed</param>
        /// <param name="methodName">the name of the method to execute</param>
        /// <param name="arguments">the methods arguments</param>
        /// <returns>the result of the executed method</returns>
        protected override ExecutionResult ExecuteMethod(string uniqueName, string methodName, object[] arguments)
        {
            if (connected)
            {
                try
                {
                    var msgStr = JsonHelper.ToJson(new InvokeMethodRequestMessage
                    {
                        MethodArguments = arguments.PackArguments(),
                        TargetMethod = methodName,
                        TargetObject = uniqueName,
                        AuthenticatedUser = identityProvider?.CurrentIdentity
                    }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true);
                    var tk = connection.InvokeServiceAsync(targetService,
                        msgStr);
                    var ret = tk.TestServerMessage<InvokeMethodResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult();
                    return new ExecutionResult
                    {
                        ActionName = methodName,
                        Parameters = ret.Arguments.UnpackArguments(),
                        Result = ret.Result.UnpackArguments().FirstOrDefault()
                    };
                }
                catch (TimeoutException tex)
                {
                    LogEnvironment.LogDebugEvent($"Error: {tex.Message}, ReConnect: {ValidateConnection()}", LogSeverity.Report);
                    throw;
                }
            }

            throw new InterProcessException("Not connected!", null);
        }

        /// <summary>
        /// Implements a method to request the execution of the demanded method with the given arguments on the remote process
        /// </summary>
        /// <param name="uniqueName">the name of the object on which the method must be executed</param>
        /// <param name="methodName">the name of the method to execute</param>
        /// <param name="arguments">the methods arguments</param
        /// <returns>the result of the executed method</returns>
        protected override async Task<ExecutionResult> ExecuteMethodAsync(string uniqueName, string methodName, object[] arguments)
        {
            if (connected)
            {
                try
                {
                    var msgStr = JsonHelper.ToJson(new InvokeMethodRequestMessage
                    {
                        MethodArguments = arguments.PackArguments(),
                        TargetMethod = methodName,
                        TargetObject = uniqueName,
                        AuthenticatedUser = identityProvider?.CurrentIdentity
                    }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true);
                    var tk = connection.InvokeServiceAsync(targetService,
                        msgStr);
                    var ret = await tk.TestServerMessage<InvokeMethodResponseMessage>(CheckConnected, CheckConnected);
                    return new ExecutionResult
                    {
                        ActionName = methodName,
                        Parameters = ret.Arguments.UnpackArguments(),
                        Result = ret.Result.UnpackArguments().FirstOrDefault()
                    };
                }
                catch(TimeoutException tex)
                {
                    LogEnvironment.LogDebugEvent($"Error: {tex.Message}, ReConnect: {ValidateConnection()}", LogSeverity.Report);
                    throw;
                }
            }

            throw new InterProcessException("Not connected!", null);
        }

        /// <summary>
        /// Registers a specific event-subscription on the server
        /// </summary>
        /// <param name="uniqueName">the unique-name of the target-object</param>
        /// <param name="eventName">the event on the target-object to subscribe to</param>
        protected override void RegisterServerEvent(string uniqueName, string eventName)
        {
            if (!useEvents)
            {
                throw new InvalidOperationException("Unable to Register an Event-Subscription in an unidirectional environment!");
            }

            if (connected)
            {
                LogEnvironment.LogEvent($@"Subscribe {eventName} on {UniqueName} for {connection.ServiceName}. TargetService: {targetService}", LogSeverity.Report);
                var msgStr = JsonHelper.ToJson(new RegisterEventRequestMessage
                {
                    TargetObject = uniqueName,
                    EventName = eventName,
                    RespondChannel = connection.ServiceName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage));
                var tk = connection.InvokeServiceAsync(targetService, msgStr);
                var ret = tk.TestServerMessage<RegisterEventResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult();
                if (!ret.Ok)
                {
                    throw new InterProcessException("Register-Event was not successful", null);
                }
            }
            else
            {
                throw new InterProcessException("Not connected!", null);
            }
        }

        /// <summary>
        /// removes a specific event-subscription on the server
        /// </summary>
        /// <param name="uniqueName">the unique-name of the target-object</param>
        /// <param name="eventName">the event on the target-object to remove the subscription for</param>
        protected override void  UnRegisterServerEvent(string uniqueName, string eventName)
        {
            if (!useEvents)
            {
                throw new InvalidOperationException("Unable to Un-Register an Event-Subscription in an unidirectional environment!");
            }

            if (connected)
            {
                var msgStr = JsonHelper.ToJson(new UnRegisterEventRequestMessage
                {
                    TargetObject = uniqueName,
                    EventName = eventName,
                    RespondChannel = connection.ServiceName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage), true);
                var tk = connection.InvokeServiceAsync(targetService, msgStr);
                var ret = tk.TestServerMessage<UnRegisterEventResponseMessage>(CheckConnected, CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult();
                if (!ret.Ok)
                {
                    throw new InterProcessException("Un-Register-Event was not successful", null);
                }
            }
            else
            {
                throw new InterProcessException("Not connected!", null);
            }
        }

        /// <summary>
        /// Tests the connection to the given target-proxy object
        /// </summary>
        /// <returns>a value indicating whether the connection is OK</returns>
        protected override bool Test()
        {
            if (connected || !initCalled)
            {
                if (!connection.Initialized)
                {
                    try
                    {
                        connection.Initialize();
                        if (isBidirectional != IsBidirectional)
                        {
                            throw new Exception("Failed to Register return-channel. Check permissions on server.");
                        }
                    }
                    finally
                    {
                        initCalled = true;
                    }
                }

                if (!connection.Operational)
                {
                    ConnectivityChanged(null, null);
                    return connected = false;
                }

                return connected = connection.DiscoverService(targetService);
            }
            else
            {
                try
                {
                    ReConnectClient();
                    //return Test();
                }
                catch
                {
                    connection?.Dispose();
                    connection = null;
                    connected = false;
                }
            }

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (useEvents)
            {
                connection.MessageArrived -= ProcessMessage;
                if (connection is not LocalServiceHubConsumer)
                {
                    connection.OperationalChanged -= ConnectivityChanged;
                }
            }

            connection.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Processes connectivity-changes of the underlaying client
        /// </summary>
        /// <param name="sender">the grpc-client that experienced a connectivity-change</param>
        /// <param name="e">empty arguments</param>
        private void ConnectivityChanged(object sender, EventArgs e)
        {
            connected = connection?.Operational??false;
            if (connection != null && !connection.Operational)
            {
                if (useEvents)
                {
                    connection.MessageArrived -= ProcessMessage;
                    connection.OperationalChanged -= ConnectivityChanged;
                }

                connection.Dispose();
                connection = null;
            }
        }

        /// <summary>
        /// Re-Initializes the client connection
        /// </summary>
        private void ReConnectClient()
        {
            connection = connector.CreateConnection();
            if (useEvents)
            {
                connection.MessageArrived += ProcessMessage;
                connection.OperationalChanged += ConnectivityChanged;
            }

            if (initCalled)
            {
                connection.Initialize();
                connected = connection.Operational;
            }
        }

        /// <summary>
        /// Processes incoming messages from the server
        /// </summary>
        /// <param name="sender">the sender of the event (normally the grpc connector)</param>
        /// <param name="e">the event-arguments that werde constructed from the server-request</param>
        private void ProcessMessage(object? sender, MessageArrivedEventArgs e)
        {
            var message = Task.FromResult(e.Message).TestClientMessage<EventNotificationMessage>(CheckConnected).ConfigureAwait(false).GetAwaiter().GetResult();
            try
            {
                RaiseEvent(message.EventName, message.Arguments.UnpackArguments());
                e.Response = JsonHelper.ToJson(message, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
            }
            catch (Exception ex)
            {
                e.Error = JsonHelper.ToJson(new ErrorResponse{SerializedException =  ex}, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
            }
            finally
            {
                e.Completed = true;
            }
        }

        private void CheckConnected(SerializedException ex)
        {
            if (ex.ContainsType("CommunicationException", "TimeoutException"))
            {
                ValidateConnection();
            }
        }

        private void CheckConnected(Exception ex)
        {
            bool lost;
            var x = ex;
            while (!(lost = x is CommunicationException || x is TimeoutException) && (x = x.InnerException) != null) ;

            if (lost)
            {
                ValidateConnection();
            }
        }
    }
}
