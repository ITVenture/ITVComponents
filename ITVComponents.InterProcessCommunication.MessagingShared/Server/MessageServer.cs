using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Factory;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages;
using ITVComponents.InterProcessCommunication.MessagingShared.Messages.ProtocolHelper;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Json;
using ITVComponents.Json.Contracts;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using Microsoft.Extensions.DependencyInjection;

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

        public MessageServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(factory, useExtendedProxying, useSecurity, security)
        {
            hubClient = new LocalServiceHubConsumer(serviceName, serviceHub, null, security);
            hubClient.MessageArrived += ClientInvokation;
        }

        public MessageServer(IServiceHubProvider serviceHub, IDictionary<string, object> exposedObjects, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security) : base(exposedObjects, useExtendedProxying, useSecurity, security)
        {
            hubClient = new LocalServiceHubConsumer(serviceName, serviceHub, null, security);
            hubClient.MessageArrived += ClientInvokation;
        }

        public MessageServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName):this(serviceHub, factory, serviceName, false,false,null)
        {
        }

        public MessageServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying):this(serviceHub, factory, serviceName, useExtendedProxying,false,null)
        {
        }

        public MessageServer(IHubConnectionFactory hubFactory, PluginFactory factory, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(factory, useExtendedProxying, useSecurity, security)
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

        static MessageServer()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether an identity the client puts into its request is used. Default: true
        /// (the behavior so far - the client's identity wins over the one the hub vouches for).
        /// </summary>
        /// <remarks>
        /// Set it to false when the hub is the only party that can vouch for a caller, e.g. a named pipe whose
        /// identity comes from the operating system. Then only the hub's identity counts, and whatever a client
        /// claims in its request is ignored.
        /// </remarks>
        public bool TrustClientIdentity { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether method arguments may only be of types registered with
        /// <see cref="JsonHelper.RegisterManualType{T}"/> (the basic types are registered by default). Default: false.
        /// </summary>
        /// <remarks>
        /// Without it, the client names the type of every argument and the server loads it. With it, an
        /// unregistered type is not loaded and the argument arrives as null.
        /// </remarks>
        public bool RestrictArgumentTypes { get; set; }

        /// <summary>
        /// Decides whether a caller may (un)subscribe events for the given back channel. Default: always.
        /// </summary>
        /// <param name="respondChannel">the back channel the events are delivered to</param>
        /// <param name="caller">the identity of the caller</param>
        /// <returns>true if the caller may use the channel</returns>
        /// <remarks>
        /// A transport that knows which back channel belongs to which caller overrides it, so that nobody can
        /// subscribe or cancel events on behalf of another client.
        /// </remarks>
        protected virtual bool MayUseEventChannel(string respondChannel, System.Security.Principal.IIdentity caller)
        {
            return true;
        }

        /// <summary>
        /// Determines the identity a request runs under.
        /// </summary>
        /// <param name="message">the request</param>
        /// <param name="e">the arguments of the arrived message, holding the identity of the hub</param>
        /// <returns>the identity of the caller, or null</returns>
        private System.Security.Principal.IIdentity CallerOf(IRequestMessage message, MessageArrivedEventArgs e)
        {
            if (TrustClientIdentity && message is AuthenticatedRequestMessage arm && arm.AuthenticatedUser != null)
            {
                return arm.AuthenticatedUser.ToIdentity() ?? e.HubUser;
            }

            if (!TrustClientIdentity && message is AuthenticatedRequestMessage ignored && ignored.AuthenticatedUser != null)
            {
                LogEnvironment.LogDebugEvent("The client sent an identity of its own; it is ignored, only the hub's identity counts.", LogSeverity.Report);
            }

            return e.HubUser;
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
            var msgStr = JsonHelper.ToJson(new EventNotificationMessage
            {
                EventName = eventName,
                Arguments = arguments.PackArguments()
            }, SerializationTypingMode.NativePolymorphism, typeof(IRequestMessage));
            var response = await hubClient.InvokeServiceAsync(sessionId, msgStr);
            var unwrapped = JsonHelper.FromJsonString<IServerResponse>(response, SerializationTypingMode.NativePolymorphism, true);
            if (unwrapped is ErrorResponse rep)
            {
                throw new InterProcessException(rep.SerializedException);
            }
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
            reconnector?.Dispose();
        }

        private void ClientInvokation(object? sender, MessageArrivedEventArgs e)
        {
            using var typeRestriction = RestrictArgumentTypes ? JsonHelper.RestrictManualTypesToRegistered() : null;
            IRequestMessage msg;
            try
            {
                msg = JsonHelper.FromJsonString<IRequestMessage>(e.Message, SerializationTypingMode.NativePolymorphism, true);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"A request could not be read and is refused: {ex.OutlineException()}", LogSeverity.Warning);
                e.Error = JsonHelper.ToJson<IServerResponse>(new ErrorResponse{SerializedException = new InvalidOperationException("The request could not be read.")}, SerializationTypingMode.NativePolymorphism);
                e.Completed = true;
                return;
            }

            LogEnvironment.LogDebugEvent($"Message is {msg}", LogSeverity.Report);
            LogEnvironment.LogDebugEvent(e.Message, LogSeverity.Report);
            IServiceProvider services = e.Services;
            try
            {
                var caller = CallerOf(msg, e);
                var requestContext = new RequestContext(msg, caller, services);
                using var scopeFactory = plugins.OpenScope(new Dictionary<string, object>
                    { { "requestContext", requestContext } }, services);
                requestContext.ScopeFactory = scopeFactory;
                if (msg is AbandonExtendedProxyRequestMessage aeprm)
                {
                    var ok = AbandonExtendedProxy(aeprm.ObjectName, requestContext);
                    e.Response = JsonHelper.ToJson(new AbandonExtendedProxyResponseMessage {Result = ok}, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                }
                else if (msg is ObjectAvailabilityRequestMessage oarm)
                {
                    var avail = CheckForAvailableProxy(oarm.UniqueName, caller, services);
                    e.Response = JsonHelper.ToJson(new ObjectAvailabilityResponseMessage
                    {
                        Message = avail.Message,
                        Available = avail.Available
                    }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                    LogEnvironment.LogDebugEvent($"Response: {e.Response}.", LogSeverity.Report);
                }
                else if (msg is SetPropertyRequestMessage sprm)
                {
                    // a client that does not pack the value sends it untyped; SetProperty converts it to the property's type then
                    var value = !string.IsNullOrEmpty(sprm.PackedValue) ? sprm.PackedValue.UnpackArguments()?.FirstOrDefault() : sprm.Value;
                    SetProperty(sprm.TargetObject, sprm.TargetMethod, sprm.MethodArguments.UnpackArguments(), value, caller, services);
                    e.Response = JsonHelper.ToJson(new SetPropertyResponseMessage
                    {
                        Ok = true
                    }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                }
                else if (msg is GetPropertyRequestMessage gprm)
                {
                    var retVal = GetProperty(gprm.TargetObject, gprm.TargetMethod, gprm.MethodArguments.UnpackArguments(), caller, services);
                    e.Response = JsonHelper.ToJson(new GetPropertyResponseMessage
                    {
                        Result = new []{retVal}.PackArguments(),
                        Arguments = gprm.MethodArguments
                    }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                }
                else if (msg is InvokeMethodRequestMessage imrm)
                {
                    var result = ExecuteMethod(imrm.TargetObject, imrm.TargetMethod, imrm.MethodArguments.UnpackArguments(), caller, services);
                    e.Response = JsonHelper.ToJson(new InvokeMethodResponseMessage
                    {
                        Arguments = imrm.MethodArguments,
                        Result = new []{result.Result}.PackArguments()
                    }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                }
                else if (msg is UnRegisterEventRequestMessage urerm)
                {
                    if (!MayUseEventChannel(urerm.RespondChannel, caller))
                    {
                        LogEnvironment.LogEvent($"{caller?.Name ?? "An anonymous caller"} wanted to cancel event {urerm.EventName} for back channel {urerm.RespondChannel}, which is not its own; refused.", LogSeverity.Warning);
                        throw new System.Security.SecurityException("The back channel does not belong to the caller.");
                    }

                    var ok = UnSubscribeEvent(urerm.TargetObject, urerm.EventName, urerm.RespondChannel, caller, services);
                    e.Response = JsonHelper.ToJson(new UnRegisterEventResponseMessage
                    {
                        Ok = ok
                    }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                }
                else if (msg is RegisterEventRequestMessage rerm)
                {
                    if (!MayUseEventChannel(rerm.RespondChannel, caller))
                    {
                        LogEnvironment.LogEvent($"{caller?.Name ?? "An anonymous caller"} wanted to subscribe event {rerm.EventName} for back channel {rerm.RespondChannel}, which is not its own; refused.", LogSeverity.Warning);
                        throw new System.Security.SecurityException("The back channel does not belong to the caller.");
                    }

                    var ok = SubscribeForEvent(rerm.TargetObject, rerm.EventName, rerm.RespondChannel, caller, services);
                    e.Response = JsonHelper.ToJson(new RegisterEventResponseMessage
                    {
                        Ok = ok
                    }, SerializationTypingMode.NativePolymorphism, typeof(IServerResponse), true);
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected message {msg?.GetType().FullName ?? "NULL"}!");
                }
            }
            catch (Exception ex)
            {
                e.Error = JsonHelper.ToJson<IServerResponse>(new ErrorResponse{SerializedException = ex}, SerializationTypingMode.NativePolymorphism);
            }
            finally
            {
                e.Completed = true;
            }
        }
    }
}
