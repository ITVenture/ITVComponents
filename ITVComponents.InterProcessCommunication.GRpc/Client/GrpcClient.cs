using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Hub;
using ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.Grpc.Messages;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.InterProcessCommunication.Grpc.Client
{
    public class GrpcClient:BaseClient
    {
        private bool isBidirectional;

        private readonly IIdentityProvider identityProvider;
        private readonly IHubConnection connection;
        private readonly string targetService;
        private readonly bool useEvents;
        private bool connected;
        public GrpcClient(string hubAddress, IHubClientConfigurator configurator, string targetService, bool useEvents)
        {
            if (!useEvents)
            {
                connection = new ServiceHubConsumer(hubAddress, configurator, targetService);
            }
            else
            {
                connection = new ServiceHubConsumer(hubAddress, $"{Guid.NewGuid()}", configurator, targetService);
                isBidirectional = true;
                connection.OperationalChanged += ConnectivityChanged;
            }

            this.targetService = targetService;
            this.useEvents = useEvents;
            if (useEvents)
            {
                connection.MessageArrived += ProcessMessage;
            }

            connected = true;
        }

        public GrpcClient(IServiceHubProvider serviceHub, string targetService, bool useEvents)
        {
            if (!useEvents)
            {
                connection = new LocalServiceHubConsumer(null, serviceHub, targetService);
            }
            else
            {
                connection = new LocalServiceHubConsumer($"{Guid.NewGuid()}", serviceHub, targetService);
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

        public GrpcClient(string hubAddress, IHubClientConfigurator configurator, string targetService, IIdentityProvider identityProvider, bool useEvents):this(hubAddress, configurator,targetService,useEvents)
        {
            this.identityProvider = identityProvider;
        }

        public GrpcClient(IServiceHubProvider serviceHub, string targetService, IIdentityProvider identityProvider, bool useEvents):this(serviceHub,targetService,useEvents)
        {
            this.identityProvider = identityProvider;
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

                var response = TestMessage<ObjectAvailabilityResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(msg, true)));
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
                return TestMessage<AbandonExtendedProxyResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(new AbandonExtendedProxyRequestMessage
                {
                    ObjectName = uniqueName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).Result;
            }

            throw new InterProcessException("Not connected!", null);
        }

        protected override object GetPropertyValue(string uniqueName, string propertyName, object[] index)
        {
            if (connected)
            {
                return TestMessage<GetPropertyResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(new GetPropertyRequestMessage
                {
                    TargetMethod = propertyName,
                    TargetObject = uniqueName,
                    MethodArguments = index,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true))).Result;
            }

            throw new InterProcessException("Not connected!", null);
        }

        protected override void SetPropertyValue(string uniqueName, string propertyName, object[] index, object value)
        {
            if (connected)
            {
                var tmp = TestMessage<SetPropertyResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(new SetPropertyRequestMessage
                {
                    TargetMethod = propertyName,
                    TargetObject = uniqueName,
                    MethodArguments = index,
                    Value = value,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true)));
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
                var ret = TestMessage<InvokeMethodResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(new InvokeMethodRequestMessage {MethodArguments = arguments, TargetMethod = methodName, TargetObject = uniqueName, AuthenticatedUser = identityProvider?.CurrentIdentity}, true)));
                return new ExecutionResult
                {
                    ActionName = methodName,
                    Parameters = ret.Arguments,
                    Result = ret.Result
                };
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
                var ret = TestMessage<InvokeMethodResponseMessage>(await connection.InvokeServiceAsync(targetService, JsonHelper.ToJsonStrongTyped(new InvokeMethodRequestMessage {MethodArguments = arguments, TargetMethod = methodName, TargetObject = uniqueName, AuthenticatedUser = identityProvider?.CurrentIdentity}, true)));
                return new ExecutionResult
                {
                    ActionName = methodName,
                    Parameters = ret.Arguments,
                    Result = ret.Result
                };
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
                var ret = TestMessage<RegisterEventResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(new RegisterEventRequestMessage
                {
                    TargetObject = uniqueName,
                    EventName = eventName,
                    RespondChannel = connection.ServiceName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true)));
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
                var ret = TestMessage<UnRegisterEventResponseMessage>(connection.InvokeService(targetService, JsonHelper.ToJsonStrongTyped(new UnRegisterEventRequestMessage
                {
                    TargetObject = uniqueName,
                    EventName = eventName,
                    RespondChannel = connection.ServiceName,
                    AuthenticatedUser = identityProvider?.CurrentIdentity
                }, true)));
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
            if (connected)
            {
                if (!connection.Initialized)
                {
                    connection.Initialize();
                    if (isBidirectional != IsBidirectional)
                    {
                        throw new Exception("Failed to Register return-channel. Check permissions on server.");
                    }
                }

                return connection.DiscoverService(targetService);
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
            LogEnvironment.LogDebugEvent($"Connectivity Changed. Current Status: {connection.Operational}", LogSeverity.Report);
            connected = connection.Operational;
        }

        /// <summary>
        /// Processes incoming messages from the server
        /// </summary>
        /// <param name="sender">the sender of the event (normally the grpc connector)</param>
        /// <param name="e">the event-arguments that werde constructed from the server-request</param>
        private void ProcessMessage(object? sender, MessageArrivedEventArgs e)
        {
            var message = TestMessage<EventNotificationMessage>(e.Message);
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
        private TExpectedType TestMessage<TExpectedType>(string message)where TExpectedType:class
        {
            var tmp = JsonHelper.FromJsonStringStrongTyped<object>(message, true);
            var ret = tmp as TExpectedType;
            if (tmp != null && ret != null)
            {
                return ret;
            }

            if (tmp is SerializedException ex)
            {
                throw new InterProcessException("Server-Operation failed!", ex);
            }

            if (tmp is Exception inex)
            {
                throw new InterProcessException("Server-Operation failed!", inex);
            }

            throw new InterProcessException($"Unexpected Response: {message}", null);
        }
    }
}
