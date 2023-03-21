using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections
{
    internal class ServiceHubConsumer : IHubConnection
    {
        private const int TickPeriod = 6000;

        private const int ReconnectPeriod = 2000;

        private readonly string serviceAddr;
        private readonly IHubClientConfigurator configurator;

        private GrpcChannel channel;

        private bool initialized;

        private ServiceHub.ServiceHubClient client;

        private CancellationTokenSource serverCallbackCancel;

        private ServiceSessionOperationMessage session;

        private Timer tickTimer;
        private RegisterServiceMessage mySvc;

        private string consumedService;

        private Random rnd;

        private bool disposing;

        //private Timer reconnector;

        private string myServiceName;
        private ICustomServerSecurity customServerSecurity;

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="serviceName">the name of this service</param>
        /// <param name="configurator">a hub-configurator that configures the channel options for this consumer object</param>
        /// <param name="consumedService">the remote-service that is being consumed</param>
        public ServiceHubConsumer(string serviceAddr, string serviceName, IHubClientConfigurator configurator, string consumedService) : this(serviceAddr, configurator, consumedService, null)
        {
            this.myServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="serviceName">the name of this service</param>
        /// <param name="configurator">a hub-configurator that configures the channel options for this consumer object</param>
        public ServiceHubConsumer(string serviceAddr, string serviceName, IHubClientConfigurator configurator, ICustomServerSecurity customServerSecurity) : this(serviceAddr, configurator, null, customServerSecurity)
        {
            this.myServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="configurator">a hub-configurator that configures the channel options for this consumer object</param>
        /// <param name="consumedService">the remote-service that is being consumed</param>
        public ServiceHubConsumer(string serviceAddr, IHubClientConfigurator configurator, string consumedService) : this(serviceAddr, configurator, consumedService, null)
        {

        }

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="configurator">a hub-configurator that configures the channel options for this consumer object</param>
        /// <param name="consumedService">the remote-service that is being consumed</param>
        /// <param name="customServerSecurity">a security-repo that allows this object to perform user-transformations</param>
        public ServiceHubConsumer(string serviceAddr, IHubClientConfigurator configurator, string consumedService, ICustomServerSecurity customServerSecurity)
        {
            this.serviceAddr = serviceAddr;
            this.configurator = configurator;
            this.consumedService = consumedService;
            this.customServerSecurity = customServerSecurity;
            tickTimer = new Timer(SendTick, null, Timeout.Infinite, Timeout.Infinite);
            //reconnector = new Timer(ReConnect, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Indicates whether this deferrable init-object is already initialized
        /// </summary>
        bool IHubConnection.Initialized => initialized;

        /// <summary>
        /// Gets the name of this Service instance
        /// </summary>
        public string ServiceName { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this object is operational
        /// </summary>
        public bool Operational { get; private set; }

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        void IHubConnection.Initialize()
        {
            if (!initialized)
            {
                try
                {
                    InitHubConnection();
                    initialized = true;
                    OnOperationalChanged(true);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Reconnect changed: {ex.Message}", LogSeverity.Error);
                }
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
            var cmt = client.ConsumeService(new ServerOperationMessage
            {
                OperationId = $"{serviceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                OperationPayload = serviceMessage,
                TargetService = serviceName,
                TickBack = false
            }, GetCallOptions());

            return cmt.ResponsePayload;
        }

        /// <summary>
        /// Invokes an action on the given ServiceName
        /// </summary>
        /// <param name="serviceName">the name of the target-service</param>
        /// <param name="serviceMessage">the message to send to the service</param>
        /// <returns>the response-message that came from the remote service</returns>
        public async Task<string> InvokeServiceAsync(string serviceName, string serviceMessage)
        {
            var cmt = await client.ConsumeServiceAsync(new ServerOperationMessage
            {
                OperationId = $"{serviceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                OperationPayload = serviceMessage,
                TargetService = serviceName,
                TickBack = false
            }, GetCallOptions());

            return cmt.ResponsePayload;
        }

        /// <summary>
        /// Checks whether the given service is available
        /// </summary>
        /// <param name="serviceName">the name of the service</param>
        /// <returns>a value indicating whether the requested service is available</returns>
        public bool DiscoverService(string serviceName)
        {
            ServiceDiscoverResponseMessage ret = null;
            try
            {
                ret = client.DiscoverService(new ServiceDiscoverMessage
                {
                    TargetService = serviceName
                }, GetCallOptions());
            }
            catch (RpcException ex)
            {
                LogEnvironment.LogEvent($"Discovery on {serviceAddr} for {serviceName} failed.", LogSeverity.Warning);
                ret = new ServiceDiscoverResponseMessage
                {
                    Ok = false,
                    Reason = ex.Message
                };
            }

            if (!ret.Ok)
            {
                LogEnvironment.LogEvent($"Service not available: {ret.Reason}", LogSeverity.Warning);
            }
            return ret.Ok;
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            if (!disposing)
            {
                disposing = true;
                tickTimer.Dispose();
                //reconnector.Dispose();
                serverCallbackCancel?.Cancel();
                channel.Dispose();
            }
        }

        /// <summary>
        /// Sends a tick request to the service
        /// </summary>
        /// <param name="state">nothing</param>
        private void SendTick(object? state)
        {
            try
            {
                client.ServiceTick(session, GetCallOptions());
            }
            catch (RpcException ex)
            {
                LogEnvironment.LogEvent($"Connection problems... {ex.Message}", LogSeverity.Error);
                LostConnection();
            }
        }

        /*/// <summary>
        /// When connection is lost, tries to reconnect to the hub
        /// </summary>
        /// <param name="state">nothing</param>
        private void ReConnect(object? state)
        {
            reconnector.Change(Timeout.Infinite, Timeout.Infinite);
            LogEnvironment.LogDebugEvent("Trying to re-connect...",LogSeverity.Report);
            CleanUp();
            try
            {
                InitHubConnection();
                initialized = true;
                OnOperationalChanged(true);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Reconnect changed: {ex.Message}", LogSeverity.Error);
                reconnector.Change(ReconnectPeriod, Timeout.Infinite);
            }
        }*/

        /// <summary>
        /// Performs a clean-up from old objects
        /// </summary>
        private void CleanUp()
        {
            try
            {
                channel?.Dispose();
                serverCallbackCancel?.Cancel();
                serverCallbackCancel?.Dispose();
            }
            catch
            {
            }
            finally
            {
                channel = null;
                client = null;
                serverCallbackCancel = null;
            }
        }

        /// <summary>
        /// should be called whenever a connection-loss occurred
        /// </summary>
        private void LostConnection()
        {
            if (ServiceName != null)
            {
                OnOperationalChanged(false);
                initialized = false;
            }
        }

        /// <summary>
        /// Initializes the hubconnection with the remote hub
        /// </summary>
        private void InitHubConnection()
        {
            CleanUp();
            AppContext.SetSwitch(
                "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            rnd = new Random();
            GrpcChannelOptions options = new GrpcChannelOptions();
            configurator.ConfigureChannel(options);
            channel = GrpcChannel.ForAddress(serviceAddr, options);
            client = new ServiceHub.ServiceHubClient(channel);
            ServiceName = myServiceName;
            if (!string.IsNullOrEmpty(ServiceName))
            {
                mySvc = new RegisterServiceMessage { ServiceName = ServiceName, Ttl = 15 };
                if (!string.IsNullOrEmpty(consumedService))
                {
                    mySvc.ResponderFor = consumedService;
                }

                var reg = client.RegisterService(mySvc, GetCallOptions());
                if (reg.Ok)
                {
                    session = new ServiceSessionOperationMessage { ServiceName = mySvc.ServiceName, SessionTicket = reg.SessionTicket, Ttl = mySvc.Ttl };
                    if (!string.IsNullOrEmpty(consumedService))
                    {
                        session.ResponderFor = consumedService;
                    }

                    tickTimer.Change(0, TickPeriod);
                    serverCallbackCancel = new CancellationTokenSource();
                    var token = serverCallbackCancel.Token;
                    Task.Run(async () =>
                    {
                        var cancelR = token;
                        var en = client.ServiceReady(session, GetCallOptions(new CallOptions(cancellationToken: cancelR)));
                        try
                        {
                            while (await en.ResponseStream.MoveNext())
                            {
                                var c = en.ResponseStream.Current;
                                if (!c.TickBack)
                                {
                                    var msg = new MessageArrivedEventArgs
                                    {
                                        Message = c.OperationPayload,
                                        TargetService = c.TargetService
                                    };

                                    if (!string.IsNullOrEmpty(c.HubUser))
                                    {
                                        msg.HubUser = JsonHelper.FromJsonStringStrongTyped<TransferIdentity>(c.HubUser).ToIdentity(customServerSecurity);
                                    }

                                    OnMessageArrived(msg);
                                    ServiceOperationResponseMessage ret;
                                    if (msg.Completed)
                                    {
                                        if (msg.Error == null)
                                        {
                                            ret = new ServiceOperationResponseMessage
                                            {
                                                OperationId = c.OperationId,
                                                ResponsePayload = msg.Response,
                                                TargetService = c.TargetService,
                                                Ok = true
                                            };
                                        }
                                        else
                                        {
                                            ret = new ServiceOperationResponseMessage
                                            {
                                                OperationId = c.OperationId,
                                                ResponsePayload = JsonHelper.ToJsonStrongTyped(msg.Error, true),
                                                TargetService = c.TargetService,
                                                Ok = false
                                            };
                                        }
                                    }
                                    else
                                    {
                                        ret = new ServiceOperationResponseMessage
                                        {
                                            OperationId = c.OperationId,
                                            TargetService = c.TargetService,
                                            ResponsePayload = JsonHelper.ToJsonStrongTyped(new SerializedException("Message was not processed!", new SerializedException[0]), true),
                                            Ok = false
                                        };
                                    }

                                    if (!string.IsNullOrEmpty(consumedService))
                                    {
                                        ret.ResponderFor = consumedService;
                                    }

                                    client.CommitServiceOperation(ret, GetCallOptions());
                                }
                                else
                                {
                                    LogEnvironment.LogDebugEvent("TickBack from Hub!", LogSeverity.Report);
                                }
                            }
                        }
                        catch (IOException ex)
                        {
                            LogEnvironment.LogDebugEvent($"Connection has gone... {ex.Message}, {ex.GetType().FullName}", LogSeverity.Warning);
                            if (!disposing)
                            {
                                LostConnection();
                            }
                        }
                        catch (RpcException ex)
                        {
                            LogEnvironment.LogDebugEvent($"Connection has gone... {ex.Message}", LogSeverity.Warning);
                            if (!disposing)
                            {
                                LostConnection();
                            }
                        }
                    }, token);
                }
                else
                {
                    ServiceName = null;
                    throw new Exception($"Unable to register Service: {reg.Reason}");
                }
            }
        }

        /// <summary>
        /// Raises the MessageArrived event
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnMessageArrived(MessageArrivedEventArgs e)
        {
            MessageArrived?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the OperationalChanged event
        /// </summary>
        protected virtual void OnOperationalChanged(bool newOperationalStatus)
        {
            if (!disposing)
            {
                Operational = newOperationalStatus;
                if (!Operational)
                {
                    tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    //reconnector.Change(ReconnectPeriod, Timeout.Infinite);
                }

                OperationalChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Creates call-options to be used for every call to the remote endpoint
        /// </summary>
        /// <returns>a call-options object that contains all custom information for the next call</returns>
        private CallOptions GetCallOptions(CallOptions baseOptions = new CallOptions())
        {
            var retVal = baseOptions;
            retVal = configurator.ConfigureCallOptions(retVal);
            return retVal;
        }

        /// <summary>
        /// Is raised when a message has arrived. A Service or client object can process the event and return an appropriate message or exception
        /// </summary>
        public event EventHandler<MessageArrivedEventArgs> MessageArrived;

        /// <summary>
        /// Is Raised when the value for the OperationalFlag has changed
        /// </summary>
        public event EventHandler OperationalChanged;
    }
}
