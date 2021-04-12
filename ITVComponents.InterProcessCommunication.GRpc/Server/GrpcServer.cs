using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub;
using ITVComponents.InterProcessCommunication.Grpc.Hub.HubConnections;
using ITVComponents.InterProcessCommunication.Grpc.Messages;
using ITVComponents.InterProcessCommunication.Shared.Base;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using ITVComponents.Plugins;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ITVComponents.InterProcessCommunication.Grpc.Server
{
    public class GrpcServer:BaseServer
    {
        private readonly IHubConnection hubClient;

        public GrpcServer(string hubAddress, PluginFactory factory, IHubClientConfigurator configurator, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(factory, useExtendedProxying, useSecurity, security)
        {
            hubClient = new ServiceHubConsumer(hubAddress, serviceName, configurator);
            hubClient.MessageArrived += ClientInvokation;
        }

        public GrpcServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying, bool useSecurity, ICustomServerSecurity security):base(factory, useExtendedProxying, useSecurity, security)
        {
            hubClient = new LocalServiceHubConsumer(serviceName, serviceHub, null);
            hubClient.MessageArrived += ClientInvokation;
        }

        public GrpcServer(string hubAddress, PluginFactory factory, IHubClientConfigurator configurator, string serviceName):this(hubAddress, factory, configurator, serviceName, false,false,null)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName):this(serviceHub, factory, serviceName, false,false,null)
        {
        }

        public GrpcServer(string hubAddress, PluginFactory factory, IHubClientConfigurator configurator, string serviceName, bool useExtendedProxying):this(hubAddress, factory, configurator, serviceName, useExtendedProxying,false,null)
        {
        }

        public GrpcServer(IServiceHubProvider serviceHub, PluginFactory factory, string serviceName, bool useExtendedProxying):this(serviceHub, factory, serviceName, useExtendedProxying,false,null)
        {
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
            if (!hubClient.Initialized)
            {
                hubClient.Initialize();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            hubClient.Dispose();
        }

        private void ClientInvokation(object? sender, MessageArrivedEventArgs e)
        {
            var msg = JsonHelper.FromJsonStringStrongTyped<object>(e.Message);
            LogEnvironment.LogDebugEvent($"Message is {msg}", LogSeverity.Report);
            LogEnvironment.LogDebugEvent(e.Message, LogSeverity.Report);
            try
            {
                if (msg is AbandonExtendedProxyRequestMessage aeprm)
                {
                    var ok = AbandonExtendedProxy(aeprm.ObjectName, aeprm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new AbandonExtendedProxyResponseMessage {Result = ok}, true);
                }
                else if (msg is ObjectAvailabilityRequestMessage oarm)
                {

                    var avail = CheckForAvailableProxy(oarm.UniqueName, oarm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new ObjectAvailabilityResponseMessage
                    {
                        Message = avail.Message,
                        Available = avail.Available
                    }, true);
                    LogEnvironment.LogDebugEvent($"Response: {e.Response}.", LogSeverity.Report);
                }
                else if (msg is SetPropertyRequestMessage sprm)
                {
                    SetProperty(sprm.TargetObject, sprm.TargetMethod, sprm.MethodArguments, sprm.Value, sprm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new SetPropertyResponseMessage
                    {
                        Ok = true
                    }, true);
                }
                else if (msg is GetPropertyRequestMessage gprm)
                {
                    var retVal = GetProperty(gprm.TargetObject, gprm.TargetMethod, gprm.MethodArguments, gprm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new GetPropertyResponseMessage
                    {
                        Result = retVal,
                        Arguments = gprm.MethodArguments
                    }, true);
                }
                else if (msg is InvokeMethodRequestMessage imrm)
                {
                    var result = ExecuteMethod(imrm.TargetObject, imrm.TargetMethod, imrm.MethodArguments, imrm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new InvokeMethodResponseMessage
                    {
                        Arguments = result.Parameters,
                        Result = result.Result
                    }, true);
                }
                else if (msg is UnRegisterEventRequestMessage urerm)
                {
                    var ok = UnSubscribeEvent(urerm.TargetObject, urerm.EventName, urerm.RespondChannel, urerm.AuthenticatedUser?.ToIdentity()??e.HubUser);
                    e.Response = JsonHelper.ToJsonStrongTyped(new UnRegisterEventResponseMessage
                    {
                        Ok = ok
                    }, true);
                }
                else if (msg is RegisterEventRequestMessage rerm)
                {
                    var ok = SubscribeForEvent(rerm.TargetObject, rerm.EventName, rerm.RespondChannel, rerm.AuthenticatedUser?.ToIdentity()??e.HubUser);
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
