using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Internal;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Messages;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.InterProcessCommunication.Shared.Security;
using ITVComponents.Logging;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualBasic;
using Exception = System.Exception;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub
{
    public class EndPointBroker
    {
        private ConcurrentDictionary<string, ConcurrentQueue<OperationWaitHandle>> messages = new ConcurrentDictionary<string, ConcurrentQueue<OperationWaitHandle>>();
        private ConcurrentDictionary<string, ServiceStatus> services = new ConcurrentDictionary<string, ServiceStatus>();
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OperationWaitHandle>> openWaitHandles = new ConcurrentDictionary<string, ConcurrentDictionary<string, OperationWaitHandle>>();
        private Random rnd = new Random();
        public EndPointBroker()
        {
        }

        public Task<ServiceOperationResponseMessage> SendMessageToServer(ServerOperationMessage message)
        {
            try
            {
                if (GrabService(message.TargetService, null, true, out var service, out var operations, out var waitingMessages))
                {
                    if (service.ServiceKind == ServiceStatus.ServiceType.Local)
                    {
                        return Task.FromResult(service.LocalClient.ProcessMessage(message));
                    }


                    OperationWaitHandle hnd = new OperationWaitHandle(message);
                    operations.Enqueue(hnd);
                    return hnd.ServerResponse.Task;

                }

                throw new CommunicationException("The requested service is not available");
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ServiceOperationResponseMessage
                {
                    OperationId = message.OperationId,
                    TargetService = message.TargetService,
                    ResponsePayload = JsonHelper.ToJsonStrongTyped((SerializedException)ex, true),
                    Ok = false
                });
            }
        }

        public ServerOperationMessage NextRequest(ServiceSessionOperationMessage serviceSession)
        {
            if (GrabService(serviceSession.ServiceName, serviceSession.SessionTicket, true, out var service, out var operations, out var waitingMessages))
            {
                if (service.ServiceKind == ServiceStatus.ServiceType.Local)
                {
                    throw new CommunicationException("Not used for local clients!");
                }

                if (operations.TryDequeue(out var retVal))
                {
                    if (!retVal.ClientRequest.TickBack)
                    {
                        waitingMessages.TryAdd(retVal.ClientRequest.OperationId, retVal);
                    }

                    return retVal.ClientRequest;
                }

                return null;
            }

            throw new CommunicationException("Unknown Service-grab - Error");
        }

        public void CommitServerOperation(ServiceOperationResponseMessage response)
        {
            if (GrabService(response.TargetService, null, true, out var service, out var operations, out var waitingMessages))
            {
                if (service != null && service.ServiceKind == ServiceStatus.ServiceType.Local)
                {
                    throw new CommunicationException("Not used for local clients!");
                }

                if (waitingMessages.TryRemove(response.OperationId, out var waitHandle))
                {
                    waitHandle.ServerResponse.SetResult(response);
                }
                else
                {
                    throw new CommunicationException("The given operation is not open.");
                }
            }
        }

        public RegisterServiceResponseMessage RegisterService(RegisterServiceMessage registration)
        {
            if (GrabService(registration.ServiceName, null, false, out var service, out var operations, out var waitingMessages))
            {
                return new RegisterServiceResponseMessage
                {
                    Reason="Service is already registered!",
                    Ok=false
                };
            }

            var newSvc = new ServiceStatus
            {
                LastPing = DateTime.Now,
                Ttl = registration.Ttl,
                ServiceName = registration.ServiceName,
                RegistrationTicket = $"{registration.ServiceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                ServiceKind = ServiceStatus.ServiceType.Grpc
            };
            bool ok =
                services.TryAdd(registration.ServiceName, newSvc) &&
                openWaitHandles.TryAdd(registration.ServiceName, new ConcurrentDictionary<string, OperationWaitHandle>()) &&
                messages.TryAdd(registration.ServiceName, new ConcurrentQueue<OperationWaitHandle>());
            var retVal = new RegisterServiceResponseMessage
            {
                SessionTicket = newSvc.RegistrationTicket,
                Ok = ok,
                Reason = ok?"":"Registration failed!"
            };
            return retVal;
        }

        internal string RegisterService(ILocalServiceClient localClient)
        {
            if (GrabService(localClient.ServiceName, null, false, out _, out _, out _))
            {
                throw new InvalidOperationException("Service is already registered!");
            }

            var newSvc = new ServiceStatus
            {
                LastPing = DateTime.Now,
                ServiceName = localClient.ServiceName,
                RegistrationTicket = $"{localClient.ServiceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                ServiceKind = ServiceStatus.ServiceType.Local,
                LocalClient = localClient
            };

            var svc =
                services.AddOrUpdate(localClient.ServiceName, s => newSvc, (s, o) =>
                {
                    LogEnvironment.LogEvent($@"Replacing old Service Registration (serviceName: {o.ServiceName}, isAlive:{o.IsAlive}, lastPing:{o.LastPing:dd.MM.yyyy HH:mm:ss}, ticket:{o.RegistrationTicket}, serviceKind:{o.ServiceKind}, Ttl:{o.Ttl})
with new Registration (serviceName: {newSvc.ServiceName}, isAlive:{newSvc.IsAlive}, lastPing:{newSvc.LastPing:dd.MM.yyyy HH:mm:ss}, ticket:{newSvc.RegistrationTicket}, serviceKind:{newSvc.ServiceKind}, Ttl:{newSvc.Ttl})", LogSeverity.Warning);
                    return newSvc;
                });
            return svc.RegistrationTicket;
        }

        public void UnRegisterService(ServiceSessionOperationMessage request)
        {
            if (GrabService(request.ServiceName, request.SessionTicket, true, out var service, out var operations, out var waitingMessages))
            {
                services.TryRemove(request.ServiceName, out _);
                messages.TryRemove(request.ServiceName, out _);
                openWaitHandles.TryRemove(request.ServiceName, out _);
                foreach (var openWaitHandle in waitingMessages)
                {
                    openWaitHandle.Value.ServerResponse.SetException(new Exception("Service is shutting down."));
                }

                while (operations.TryDequeue(out var msg))
                {
                    msg.ServerResponse.SetException(new Exception("Service is shutting down."));
                }
            }
        }

        internal void UnRegisterService(ILocalServiceClient service)
        {
            if (GrabService(service.ServiceName, null, false, out var svc, out var operations, out var waitingMessages))
            {
                services.TryRemove(service.ServiceName, out _);
            }

            throw new CommunicationException("Service is unknown!");
        }

        public ServiceTickResponseMessage Tick(ServiceSessionOperationMessage request)
        {
            LogEnvironment.LogDebugEvent($"Submitted Ticket: {request.SessionTicket}", LogSeverity.Report);
            if (GrabService(request.ServiceName, request.SessionTicket, true, out var service, out var operations, out var waitingMessages))
            {
                if (service.ServiceKind == ServiceStatus.ServiceType.Local)
                {
                    throw new CommunicationException("Use the overload for local services!");
                }

                LogEnvironment.LogDebugEvent($"Ticket: {service.RegistrationTicket}", LogSeverity.Report);
                service.LastPing = DateTime.Now;
                operations.Enqueue(new OperationWaitHandle(new ServerOperationMessage {TickBack = true}));
                return new ServiceTickResponseMessage
                {
                    Ok=true,
                    PendingOperationsCount = operations.Count
                };
            }

            throw new CommunicationException("Unknown Service-grab - Error");
        }

        public ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage request)
        {
            var retVal = new ServiceDiscoverResponseMessage
            {
                TargetService = request.TargetService,
                Ok = false,
                Reason="Service is not available",
            };
            if (GrabService(request.TargetService, null, false, out var service, out var operations, out var waitingMessages))
            {
                retVal.Reason = string.Empty;
                retVal.Ok = true;
            }

            return retVal;
        }

        private bool GrabService(string serviceName, string serviceTicket, bool throwWhenInvalid, out ServiceStatus serviceStatus, out ConcurrentQueue<OperationWaitHandle> pendingOps, out ConcurrentDictionary<string, OperationWaitHandle> waitingMessages)
        {
            if (!services.TryGetValue(serviceName, out serviceStatus))
            {
                if (!throwWhenInvalid)
                {
                    pendingOps = null;
                    waitingMessages = null;
                    return false;
                }

                throw new CommunicationException("Service is unknown!");
            }

            if (serviceStatus.ServiceKind == ServiceStatus.ServiceType.Local)
            {
                pendingOps = null;
                waitingMessages = null;
                return true;
            }

            if (!serviceStatus.IsAlive)
            {
                if (!throwWhenInvalid)
                {
                    pendingOps = null;
                    waitingMessages = null;
                    return false;
                }

                throw new CommunicationException("Service-connection has timed out!");
            }

            if (!string.IsNullOrEmpty(serviceTicket) && serviceTicket != serviceStatus.RegistrationTicket)
            {
                if (!throwWhenInvalid)
                {
                    pendingOps = null;
                    waitingMessages = null;
                    return false;
                }

                throw new CommunicationException("Session-Ticket is invalid!");
            }

            if (!openWaitHandles.TryGetValue(serviceName, out waitingMessages))
            {
                throw new CommunicationException("Service is broken!");
            }

            if (!messages.TryGetValue(serviceName, out pendingOps))
            {
                throw new CommunicationException("Service is broken!");
            }

            return true;
        }
    }
}
