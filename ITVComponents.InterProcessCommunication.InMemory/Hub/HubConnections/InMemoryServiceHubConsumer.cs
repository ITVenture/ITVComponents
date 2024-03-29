﻿using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Client;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Factory;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.HubConnections
{
    public class InMemoryServiceHubConsumer : IHubConnection
    {
        private const int TickPeriod = 6000;

        private const int ReconnectPeriod = 2000;

        private readonly string serviceAddr;

        private bool initialized;

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

        private IHubFactory factory;

        private IServiceHubClientChannel client;

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="serviceName">the name of this service</param>
        /// <param name="factory">a hub-connection factory that configures the channel options for this consumer object</param>
        /// <param name="consumedService">the remote-service that is being consumed</param>
        public InMemoryServiceHubConsumer(string serviceAddr, string serviceName, IHubFactory factory, string consumedService) : this(serviceAddr, factory, consumedService, null)
        {
            this.myServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="serviceName">the name of this service</param>
        /// <param name="factory">a hub-connection factory that configures the channel options for this consumer object</param>
        public InMemoryServiceHubConsumer(string serviceAddr, string serviceName, IHubFactory factory, ICustomServerSecurity customServerSecurity) : this(serviceAddr, factory, null, customServerSecurity)
        {
            this.myServiceName = serviceName;
        }

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="factory">a hub-connection factory that configures the channel options for this consumer object</param>
        /// <param name="consumedService">the remote-service that is being consumed</param>
        public InMemoryServiceHubConsumer(string serviceAddr, IHubFactory factory, string consumedService) : this(serviceAddr, factory, consumedService, null)
        {

        }

        /// <summary>
        /// Initializes a new instance of the ServiceConsumer class
        /// </summary>
        /// <param name="serviceAddr">the address of the Hub</param>
        /// <param name="factory">a hub-connection factory that configures the channel options for this consumer object</param>
        /// <param name="consumedService">the remote-service that is being consumed</param>
        /// <param name="customServerSecurity">a security-repo that allows this object to perform user-transformations</param>
        public InMemoryServiceHubConsumer(string serviceAddr, IHubFactory factory, string consumedService, ICustomServerSecurity customServerSecurity)
        {
            channel = new AsyncBackStream();
            this.serviceAddr = serviceAddr;
            this.factory = factory;
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
        /// a back channel that allows this HubConsumer to receive messages from its client
        /// </summary>
        private AsyncBackStream channel;

        /// <summary>
        /// Initializes this deferred initializable object
        /// </summary>
        void IHubConnection.Initialize()
        {
            if (!initialized && !disposing)
            {
                try
                {
                    InitHubConnection();
                    initialized = true;
                    OnOperationalChanged(true);
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogEvent($"Reconnect changed: {ex.OutlineException()}", LogSeverity.Error);
                    /*if (!disposing)
                    {
                        reconnector.Change(ReconnectPeriod, Timeout.Infinite);
                    }*/
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
            });

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
            });

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
                });
            }
            catch (CommunicationException ex)
            {
                LogEnvironment.LogEvent($"Discovery on {serviceAddr} for {serviceName} failed.", LogSeverity.Warning);
                ret = new ServiceDiscoverResponseMessage
                {
                    Ok = false,
                    Reason = ex.Message
                };
            }
            catch (Exception ex)
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
            if (!disposing && initialized)
            {
                disposing = true;
                tickTimer.Dispose();
                //reconnector.Dispose();
                client.Broken -= ClientBroken;
                client.Dispose();
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
                client.ServiceTick(new ServiceSessionOperationMessage
                {
                    ResponderFor = session.ResponderFor,
                    ServiceName = session.ServiceName,
                    SessionTicket = session.SessionTicket,
                    Tick=true,
                    Ttl = session.Ttl
                });
            }
            catch (CommunicationException ex)
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
            LogEnvironment.LogDebugEvent("Trying to re-connect...", LogSeverity.Report);
            CleanUp();
            try
            {
                InitHubConnection();
                initialized = true;
                OnOperationalChanged(true);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Reconnect changed: {ex.OutlineException()}", LogSeverity.Error);
                reconnector.Change(ReconnectPeriod, Timeout.Infinite);
            }
        }*/

        /*/// <summary>
        /// Performs a clean-up from old objects
        /// </summary>
        private void CleanUp()
        {
            try
            {
                client.Dispose();
                channel?.Dispose();
                serverCallbackCancel.Cancel();
                serverCallbackCancel.Dispose();
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
        }*/

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
            rnd = new Random();
            client = factory.CreateClient(serviceAddr, channel);
            client.Broken += ClientBroken;
            ServiceName = myServiceName;
            if (!string.IsNullOrEmpty(ServiceName))
            {
                mySvc = new RegisterServiceMessage { ServiceName = ServiceName, Ttl = 15 };
                if (!string.IsNullOrEmpty(consumedService))
                {
                    mySvc.ResponderFor = consumedService;
                }

                var reg = client.RegisterService(mySvc);
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
                        try
                        {
                            client.ServiceReady(session);
                            LogEnvironment.LogDebugEvent("Service is ready!", LogSeverity.Report);
                            while (await channel.MoveNext().ConfigureAwait(false))
                            {
                                var c = channel.Current;
                                LogEnvironment.LogDebugEvent(
                                    $"TargetService: {c.TargetService}, TickBack: {c.TickBack}, Id: {c.OperationId}",
                                    LogSeverity.Report);
                                if (!c.TickBack)
                                {
                                    var msg = new MessageArrivedEventArgs
                                    {
                                        Message = c.OperationPayload,
                                        TargetService = c.TargetService
                                    };

                                    if (!string.IsNullOrEmpty(c.HubUser))
                                    {
                                        msg.HubUser = JsonHelper.FromJsonStringStrongTyped<TransferIdentity>(c.HubUser)
                                            .ToIdentity(customServerSecurity);
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
                                            ResponsePayload = JsonHelper.ToJsonStrongTyped(
                                                new SerializedException("Message was not processed!",
                                                    new SerializedException[0]), true),
                                            Ok = false
                                        };
                                    }

                                    if (!string.IsNullOrEmpty(consumedService))
                                    {
                                        ret.ResponderFor = consumedService;
                                    }

                                    client.CommitServiceOperation(ret);
                                }
                                else
                                {
                                    LogEnvironment.LogDebugEvent("TickBack from Hub!", LogSeverity.Report);
                                }
                            }
                        }
                        catch (IOException ex)
                        {
                            LogEnvironment.LogDebugEvent(
                                $"Connection has gone... {ex.Message}, {ex.GetType().FullName}", LogSeverity.Warning);
                        }
                        catch (CommunicationException ex)
                        {
                            LogEnvironment.LogDebugEvent($"Connection has gone... {ex.Message}", LogSeverity.Warning);
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogEvent($"Unexpected Error: {ex.OutlineException()}", LogSeverity.Error);
                        }
                        finally
                        {
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

        private void ClientBroken(object sender, EventArgs e)
        {
            OnOperationalChanged(false);
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
        /// Is raised when a message has arrived. A Service or client object can process the event and return an appropriate message or exception
        /// </summary>
        public event EventHandler<MessageArrivedEventArgs> MessageArrived;

        /// <summary>
        /// Is Raised when the value for the OperationalFlag has changed
        /// </summary>
        public event EventHandler OperationalChanged;
    }
}
