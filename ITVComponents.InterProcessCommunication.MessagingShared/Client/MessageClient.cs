using System;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;

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

        public MessageClient(IServiceHubProvider serviceHub, string targetService, bool useEvents)
        {
            if (!useEvents)
            {
                connection = new LocalServiceHubConsumer(null, serviceHub, targetService, null);
            }
            else
            {
                connection = new LocalServiceHubConsumer($"{Guid.NewGuid()}", serviceHub, targetService, null);
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

        protected MessageClient(IHubConnectionFactory connector, string targetService, IIdentityProvider identityProvider, bool  useEvents)
        {
            this.connector = connector;
            this.targetService = targetService;
            isBidirectional = useEvents;
            this.useEvents = useEvents;
            this.identityProvider = identityProvider;
            ReConnectClient();
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

                var response = TestMessage<ObjectAvailabilityResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(msg, true))).ConfigureAwait(false).GetAwaiter().GetResult();
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
                return TestMessage<AbandonExtendedProxyResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new AbandonExtendedProxyRequestMessage
                {
                    ObjectName = uniqueName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).ConfigureAwait(false).GetAwaiter().GetResult().Result;
            }

            throw new InterProcessException("Not connected!", null);
        }

        protected override object GetPropertyValue(string uniqueName, string propertyName, object[] index)
        {
            if (connected)
            {
                return TestMessage<GetPropertyResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new GetPropertyRequestMessage
                {
                    TargetMethod = propertyName,
                    TargetObject = uniqueName,
                    MethodArguments = index,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).ConfigureAwait(false).GetAwaiter().GetResult().Result;
            }

            throw new InterProcessException("Not connected!", null);
        }

        protected override void SetPropertyValue(string uniqueName, string propertyName, object[] index, object value)
        {
            if (connected)
            {
                var tmp = TestMessage<SetPropertyResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new SetPropertyRequestMessage
                {
                    TargetMethod = propertyName,
                    TargetObject = uniqueName,
                    MethodArguments = index,
                    Value = value,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).ConfigureAwait(false).GetAwaiter().GetResult();
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
                    var ret = TestMessage<InvokeMethodResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new InvokeMethodRequestMessage { MethodArguments = arguments, TargetMethod = methodName, TargetObject = uniqueName, AuthenticatedUser = identityProvider?.CurrentIdentity }, true))).ConfigureAwait(false).GetAwaiter().GetResult();
                    return new ExecutionResult
                    {
                        ActionName = methodName,
                        Parameters = ret.Arguments,
                        Result = ret.Result
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
                    var ret = await TestMessage<InvokeMethodResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new InvokeMethodRequestMessage { MethodArguments = arguments, TargetMethod = methodName, TargetObject = uniqueName, AuthenticatedUser = identityProvider?.CurrentIdentity }, true)));
                    return new ExecutionResult
                    {
                        ActionName = methodName,
                        Parameters = ret.Arguments,
                        Result = ret.Result
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
                var ret = TestMessage<RegisterEventResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new RegisterEventRequestMessage
                {
                    TargetObject = uniqueName,
                    EventName = eventName,
                    RespondChannel = connection.ServiceName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).ConfigureAwait(false).GetAwaiter().GetResult();
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
                var ret = TestMessage<UnRegisterEventResponseMessage>(connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new UnRegisterEventRequestMessage
                {
                    TargetObject = uniqueName,
                    EventName = eventName,
                    RespondChannel = connection.ServiceName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).ConfigureAwait(false).GetAwaiter().GetResult();
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
            var message = TestMessage<EventNotificationMessage>(Task.FromResult(e.Message)).ConfigureAwait(false).GetAwaiter().GetResult();
            try
            {
                RaiseEvent(message.EventName, message.Arguments);
                e.Response = JsonHelper.ToJsonStrongTyped(message, true);
            }
            catch (Exception ex)
            {
                e.Error = ex;
            }
            finally
            {
                e.Completed = true;
            }
        }

        /// <summary>
        /// Tests the received message and returns it, if its the expected type or throws an exception otherwise
        /// </summary>
        /// <typeparam name="TExpectedType">the expected incoming type</typeparam>
        /// <param name="message">the received message</param>
        /// <returns>the parsed message</returns>
        private async Task<TExpectedType> TestMessage<TExpectedType>(Task<string> msgFunc)where TExpectedType:class
        {
            object tmp = null;
            string message = null;
            try
            {
                message = await msgFunc;
                tmp = JsonHelper.FromJsonStringStrongTyped<object>(message, true);
                var ret = tmp as TExpectedType;
                if (tmp != null && ret != null)
                {
                    return ret;
                }
            }
            catch(Exception exx)
            {
                LogEnvironment.LogDebugEvent(null, $"Error processing message: {exx.OutlineException()}",
                    (int)LogSeverity.Error, "ITVComponents.IPC.MS.MessageClient");
                tmp = exx;
            }

            if (tmp is SerializedException ex)
            {
                CheckConnected(ex);
                throw new InterProcessException("Server-Operation failed!", ex);
            }

            if (tmp is Exception inex)
            {
                CheckConnected(inex);
                throw new InterProcessException("Server-Operation failed!", inex);
            }

            throw new InterProcessException($"Unexpected Response: {message}", null);
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
