using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Server
{
    public class MessageServer:BaseServer
    {
        private int reconnectTimeout = 1500;
        private IHubConnection hubClient;
        private readonly IHubConnectionFactory hubFactory;
        private bool initCalled = false;
        private Timer reconnector;
        private object reconnLock = new object();

        public MessageServer(IServiceHubProvider serviceHub, IPluginFactory factory, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(factory, useExtendedProxying, useSecurity, security)
        {
            hubClient = new LocalServiceHubConsumer(serviceName, serviceHub, null, security);
            hubClient.MessageArrived += ClientInvokation;
        }

        public MessageServer(IServiceHubProvider serviceHub, IDictionary<string, object> exposedObjects, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security) : base(exposedObjects, useExtendedProxying, useSecurity, security)
        {
            hubClient = new LocalServiceHubConsumer(serviceName, serviceHub, null, security);
            hubClient.MessageArrived += ClientInvokation;
        }

        public MessageServer(IServiceHubProvider serviceHub, IPluginFactory factory, string serviceName):this(serviceHub, factory, serviceName, false,false,null)
        {
        }

        public MessageServer(IServiceHubProvider serviceHub, IPluginFactory factory, string serviceName, bool useExtendedProxying):this(serviceHub, factory, serviceName, useExtendedProxying,false,null)
        {
        }

        public MessageServer(IHubConnectionFactory hubFactory, IPluginFactory factory, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(factory, useExtendedProxying, useSecurity, security)
        {
            this.hubFactory = hubFactory;
            reconnector = new Timer(TryReconnect, null, Timeout.Infinite, Timeout.Infinite);
            ConnectToHub();
        }

        public MessageServer(IHubConnectionFactory hubFactory, IDictionary<string, object> exposedObjects, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security) : base(exposedObjects, useExtendedProxying, useSecurity, security)
        {
            this.hubFactory = hubFactory;
            reconnector = new Timer(TryReconnect, null, Timeout.Infinite, Timeout.Infinite);
            ConnectToHub();
        }

        public MessageServer(IHubConnectionFactory hubFactory, IFactoryWrapper factoryWrapper, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security) : base(factoryWrapper, useExtendedProxying, useSecurity, security)
        {
            this.hubFactory = hubFactory;
            reconnector = new Timer(TryReconnect, null, Timeout.Infinite, Timeout.Infinite);
            ConnectToHub();
        }

        public MessageServer(IServiceHubProvider serviceHub, IFactoryWrapper factoryWrapper, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security) : base(factoryWrapper, useExtendedProxying, useSecurity, security)
        {
            hubClient = new LocalServiceHubConsumer(serviceName, serviceHub, null, security);
            hubClient.MessageArrived += ClientInvokation;
        }

        private void TryReconnect(object state)
        {
            if (Monitor.TryEnter(reconnLock))
            {
                try
                {
                    if (hubClient != null && !hubClient.Operational)
                    {
                        try
                        {
                            hubClient.MessageArrived -= ClientInvokation;
                            if (hubClient is not LocalServiceHubConsumer)
                            {
                                hubClient.OperationalChanged -= ConnectedChanges;
                            }

                            hubClient.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogEnvironment.LogDebugEvent($"Un-Expected Disconnection Error: {ex.OutlineException()}", LogSeverity.Error);
                        }
                        finally
                        {
                            hubClient = null;
                        }
                    }
                    if (hubClient == null)
                    {
                        try
                        {
                            ConnectToHub();
                        }
                        catch(Exception ex)
                        {
                            LogEnvironment.LogEvent($"Error when connecting to hub: {ex.OutlineException()}", LogSeverity.Warning);
                        }
                    }
                    else
                    {
                        reconnector.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                }
                finally
                {
                    Monitor.Exit(reconnLock);
                }
            }
        }

        private void ConnectedChanges(object sender, EventArgs e)
        {
            if (hubClient != null && !hubClient.Operational)
            {
                try
                {
                    hubClient.MessageArrived -= ClientInvokation;
                    if (hubClient is not LocalServiceHubConsumer)
                    {
                        hubClient.OperationalChanged -= ConnectedChanges;
                    }

                    hubClient.Dispose();
                }
                catch (Exception ex)
                {
                    LogEnvironment.LogDebugEvent($"Un-Expected Disconnection Error: {ex.OutlineException()}", LogSeverity.Error);
                }
                finally
                {
                    hubClient = null;
                    reconnector.Change(reconnectTimeout, reconnectTimeout);
                }
            }
        }

        private void ConnectToHub()
        {
            try
            {
                hubClient = hubFactory.CreateConnection();
                hubClient.MessageArrived += ClientInvokation;
                hubClient.OperationalChanged += ConnectedChanges;
                if (initCalled)
                {
                    hubClient.Initialize();
                }
            }
            catch(Exception ex)
            {
                LogEnvironment.LogEvent($"Error connecting to hub: {ex.OutlineException()}", LogSeverity.Warning);
                throw;
            }
            finally
            {
                if (!hubClient.Operational && initCalled)
                {
                    reconnector.Change(reconnectTimeout, reconnectTimeout);
                }
                else
                {
                    reconnector.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Raises an event on the client
        /// </summary>
        /// <param name="eventName">the name of the server-event</param>
        /// <param name="sessionId">the session for which to raise the event</param>
        /// <param name="arguments">the arguments for the raised event</param>
        protected override async Task RaiseEvent(string eventName, string sessionId, object[] arguments)
        {
            var response = await hubClient.InvokeServiceAsync(sessionId, JsonHelper.ToJsonStrongTyped(new EventNotificationMessage
            {
                EventName = eventName,
                Arguments = arguments
            }));
        }

        /// <summary>
        /// Invokes a test-method on the event-subscribing client
        /// </summary>
        /// <param name="sessionId">the session id for which to check whether the client is still present</param>
        /// <returns>a value indicating whether the client is still active</returns>
        protected override bool Test(string sessionId)
        {
            return hubClient.DiscoverService(sessionId);
        }

        /// <summary>
        /// Initializes this object to act as a ipc service
        /// </summary>
        protected override void ServiceInit()
        {
            if (!initCalled)
            {
                try
                {
                    hubClient.Initialize();
                    if (!hubClient.Operational)
                    {
                        reconnector.Change(reconnectTimeout, reconnectTimeout);
                    }
                }
                catch(Exception ex)
                {
                    LogEnvironment.LogEvent($"Error connecting to hub: {ex.OutlineException()}", LogSeverity.Warning);
                    reconnector.Change(reconnectTimeout, reconnectTimeout);
                }
                finally
                {
                    initCalled = true;
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            hubClient?.Dispose();
            hubClient = null;
            reconnector.Dispose();
        }

        private void ClientInvokation(object? sender, MessageArrivedEventArgs e)
        {
            var msg = JsonHelper.FromJsonStringStrongTyped<object>(e.Message);
            LogEnvironment.LogDebugEvent($"Message is {msg}", LogSeverity.Report);
            LogEnvironment.LogDebugEvent(e.Message, LogSeverity.Report);
            IServiceProvider services = e.Services;
            try
            {
                if (msg is AbandonExtendedProxyRequestMessage aeprm)
                {
                    var ok = AbandonExtendedProxy(aeprm.ObjectName, aeprm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new AbandonExtendedProxyResponseMessage {Result = ok}, true);
                }
                else if (msg is ObjectAvailabilityRequestMessage oarm)
                {
                    var avail = CheckForAvailableProxy(oarm.UniqueName, oarm.AuthenticatedUser?.ToIdentity()??e.HubUser, services);
                    e.Response = JsonHelper.ToJsonStrongTyped(new ObjectAvailabilityResponseMessage
                    {
                        Message = avail.Message,
                        Available = avail.Available
                    }, true);
                    LogEnvironment.LogDebugEvent($"Response: {e.Response}.", LogSeverity.Report);
                }
                else if (msg is SetPropertyRequestMessage sprm)
                {
                    SetProperty(sprm.TargetObject, sprm.TargetMethod, sprm.MethodArguments, sprm.Value, sprm.AuthenticatedUser?.ToIdentity()??e.HubUser, services);
                    e.Response = JsonHelper.ToJsonStrongTyped(new SetPropertyResponseMessage
                    {
                        Ok = true
                    }, true);
                }
                else if (msg is GetPropertyRequestMessage gprm)
                {
                    var retVal = GetProperty(gprm.TargetObject, gprm.TargetMethod, gprm.MethodArguments, gprm.AuthenticatedUser?.ToIdentity()??e.HubUser, services);
                    e.Response = JsonHelper.ToJsonStrongTyped(new GetPropertyResponseMessage
                    {
                        Result = retVal,
                        Arguments = gprm.MethodArguments
                    }, true);
                }
                else if (msg is InvokeMethodRequestMessage imrm)
                {
                    var result = ExecuteMethod(imrm.TargetObject, imrm.TargetMethod, imrm.MethodArguments, imrm.AuthenticatedUser?.ToIdentity()??e.HubUser, services);
                    e.Response = JsonHelper.ToJsonStrongTyped(new InvokeMethodResponseMessage
                    {
                        Arguments = result.Parameters,
                        Result = result.Result
                    }, true);
                }
                else if (msg is UnRegisterEventRequestMessage urerm)
                {
                    var ok = UnSubscribeEvent(urerm.TargetObject, urerm.EventName, urerm.RespondChannel, urerm.AuthenticatedUser?.ToIdentity()??e.HubUser, services);
                    e.Response = JsonHelper.ToJsonStrongTyped(new UnRegisterEventResponseMessage
                    {
                        Ok = ok
                    }, true);
                }
                else if (msg is RegisterEventRequestMessage rerm)
                {
                    var ok = SubscribeForEvent(rerm.TargetObject, rerm.EventName, rerm.RespondChannel, rerm.AuthenticatedUser?.ToIdentity()??e.HubUser, services);
                    e.Response = JsonHelper.ToJsonStrongTyped(new RegisterEventResponseMessage
                    {
                        Ok = ok
                    }, true);
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected message {msg?.GetType().FullName ?? "NULL"}!");
                }
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
    }
}
