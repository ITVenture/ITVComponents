using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Internal;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.Grpc.Hub.WebToolkitOverrides;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections
{
    internal class LocalServiceHubConsumer:IHubConnection, ILocalServiceClient
    {
        private readonly IServiceHubProvider localProvider;
        private bool initialized;
        private Random rnd;
        private string consumedService;
        private readonly ICustomServerSecurity serverSecurity;

        /// <summary>
        /// Initializes a new instance of the LocalServiceHubConsumer class
        /// </summary>
        /// <param name="serviceName">the local service name</param>
        /// <param name="localProvider">the local serviceHubProvider instance</param>
        public LocalServiceHubConsumer(string serviceName, IServiceHubProvider localProvider, string consumedService, ICustomServerSecurity serverSecurity)
        {
            this.localProvider = localProvider;
            ServiceName = serviceName;
            this.consumedService = consumedService;
            this.serverSecurity = serverSecurity;
            rnd = new Random();
            if (!string.IsNullOrEmpty(serviceName) && !string.IsNullOrEmpty(consumedService))
            {
                TemporaryGrants.GrantTemporaryPermission(consumedService, serviceName);
            }
        }

        /// <summary>
        /// Gets the ServiceName behind this HubConnection
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets a value indicating whether this hubConnection was initialized
        /// </summary>
        bool IHubConnection.Initialized => initialized;

        /// <summary>
        /// Processes a message that was received from a remote client
        /// </summary>
        /// <param name="message">the message that was received from the remote host</param>
        /// <returns>a response message that was generated as result of the received message</returns>
        public ServiceOperationResponseMessage ProcessMessage(ServerOperationMessage message)
        {
            var msg = new MessageArrivedEventArgs
            {
                TargetService = message.TargetService,
                Message = message.OperationPayload
            };

            if (!string.IsNullOrEmpty(message.HubUser))
            {
                msg.HubUser = JsonHelper.FromJsonStringStrongTyped<TransferIdentity>(message.HubUser).ToIdentity(serverSecurity);
            }

            OnMessageArrived(msg);
            ServiceOperationResponseMessage ret;
            if (msg.Completed)
            {
                if (msg.Error == null)
                {
                    ret = new ServiceOperationResponseMessage
                    {
                        OperationId = message.OperationId,
                        ResponsePayload = msg.Response,
                        TargetService = message.TargetService,
                        Ok=true
                    };
                }
                else
                {
                    ret = new ServiceOperationResponseMessage
                    {
                        OperationId = message.OperationId,
                        ResponsePayload = JsonHelper.ToJsonStrongTyped(msg.Error, true),
                        TargetService = message.TargetService,
                        Ok = false
                    };
                }
            }
            else
            {
                ret = new ServiceOperationResponseMessage
                {
                    OperationId = message.OperationId,
                    TargetService = message.TargetService,
                    ResponsePayload = JsonHelper.ToJsonStrongTyped(new SerializedException("Message was not processed!", new SerializedException[0]), true),
                    Ok = false
                };
            }

            if (!string.IsNullOrEmpty(consumedService))
            {
                ret.ResponderFor = consumedService;
            }

            return ret;
        }

        /// <summary>
        /// Initializes this ServiceHub instance
        /// </summary>
        void IHubConnection.Initialize()
        {
            if (!string.IsNullOrEmpty(ServiceName))
            {
                localProvider.Broker.RegisterService(this);
            }
        }

        /// <summary>
        /// Invokes an action on the given ServiceName
        /// </summary>
        /// <param name="serviceName">the name of the target-service</param>
        /// <param name="serviceMessage">the message to send to the service</param>
        /// <returns>the response-message that came from the remote service</returns>
        public string InvokeService(string serviceName, string serviceMessage)
        {
            var awaiter = localProvider.Broker.SendMessageToServer(new ServerOperationMessage
            {
                OperationId = $"{serviceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                OperationPayload = serviceMessage,
                TargetService = serviceName,
                TickBack=false
            }).ConfigureAwait(false).GetAwaiter();
            return awaiter.GetResult().ResponsePayload;
        }

        /// <summary>
        /// Invokes an action on the given ServiceName
        /// </summary>
        /// <param name="serviceName">the name of the target-service</param>
        /// <param name="serviceMessage">the message to send to the service</param>
        /// <returns>the response-message that came from the remote service</returns>
        public async Task<string> InvokeServiceAsync(string serviceName, string serviceMessage)
        {
            var result = await localProvider.Broker.SendMessageToServer(new ServerOperationMessage
            {
                OperationId = $"{serviceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                OperationPayload = serviceMessage,
                TargetService = serviceName,
                TickBack=false
            }).ConfigureAwait(false);
            return result.ResponsePayload;
        }

        /// <summary>
        /// Checks whether the given service is available
        /// </summary>
        /// <param name="serviceName">the name of the service</param>
        /// <returns>a value indicating whether the requested service is available</returns>
        public bool DiscoverService(string serviceName)
        {
            return localProvider.Broker.DiscoverService(new ServiceDiscoverMessage
            {
                TargetService = serviceName
            }).Ok;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            if (initialized && !string.IsNullOrEmpty(ServiceName))
            {
                localProvider.Broker.UnRegisterService(this);
                if (!string.IsNullOrEmpty(consumedService) && !string.IsNullOrEmpty(ServiceName))
                {
                    TemporaryGrants.RevokeTemporaryPermission(consumedService, ServiceName);
                }
            }
        }

        /// <summary>
        /// Raises the MessageArrived event
        /// </summary>
        /// <param name="e">the arguments for the arrived message</param>
        protected virtual void OnMessageArrived(MessageArrivedEventArgs e)
        {
            MessageArrived?.Invoke(this, e);
        }

        /// <summary>
        /// Is raised when a message has arrived. A Service or client object can process the event and return an appropriate message or exception
        /// </summary>
        public event EventHandler<MessageArrivedEventArgs> MessageArrived;

        /// <summary>
        /// Gets a value indicating whether this object is operational irrelevant for local connections
        /// </summary>
        public bool Operational { get; } = true;

        /// <summary>
        /// Is Raised when the value for the OperationalFlag has changed
        /// </summary>
        public event EventHandler OperationalChanged;
    }
}
