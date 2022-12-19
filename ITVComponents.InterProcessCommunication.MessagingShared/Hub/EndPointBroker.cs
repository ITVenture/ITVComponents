using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Internal;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Proxy;
using ITVComponents.InterProcessCommunication.Shared.Helpers;
using ITVComponents.Logging;
using Exception = System.Exception;

namespace ITVComponents.InterProcessCommunication.MessagingShared.Hub
{
    public class EndPointBroker : IDisposable, IEndPointBroker
    {
        private ConcurrentDictionary<string, ConcurrentQueue<OperationWaitHandle>> messages = new ConcurrentDictionary<string, ConcurrentQueue<OperationWaitHandle>>();
        private ConcurrentDictionary<string, ServiceStatus> services = new ConcurrentDictionary<string, ServiceStatus>();
        private ConcurrentDictionary<string, ConcurrentDictionary<string, OperationWaitHandle>> openWaitHandles = new ConcurrentDictionary<string, ConcurrentDictionary<string, OperationWaitHandle>>();
        private Random rnd = new Random();
        private Timer tickOpenWaits;
        private object registrationLock = new object();
        private ConcurrentDictionary<string, IEndPointBroker> children = new ConcurrentDictionary<string, IEndPointBroker>();
        public EndPointBroker()
        {
            tickOpenWaits = new Timer(TickOpenWaits, null, Timeout.Infinite, Timeout.Infinite);
            tickOpenWaits.Change(0, 5000);
        }

        public Task<ServiceOperationResponseMessage> SendMessageToServer(ServerOperationMessage message, IServiceProvider services)
        {
            try
            {
                if (!IsRemoteMessage(message, out var remoteBroker))
                {
                    if (GrabService(message.TargetService, null, true, out var service, out var operations,
                            out var waitingMessages))
                    {
                        if (service.ServiceKind == ServiceStatus.ServiceType.Local)
                        {
                            return Task.FromResult(service.LocalClient.ProcessMessage(message, services));
                        }

                        lock (service)
                        {
                            OperationWaitHandle hnd = new OperationWaitHandle(message);
                            try
                            {
                                if (service.OpenTaskWait != null)
                                {
                                    var t = service.OpenTaskWait;
                                    service.OpenTaskWait = null;
                                    t.SetResult(hnd);
                                }
                                else
                                {
                                    operations.Enqueue(hnd);
                                }
                            }
                            catch (Exception ex)
                            {
                                hnd.ServerResponse.SetException(ex);
                            }


                            return hnd.ServerResponse.Task;
                        }

                    }

                    throw new CommunicationException("The requested service is not available");
                }
                else
                {
                    return remoteBroker.SendMessageToServer(message, services);
                }
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

        /// <summary>
        /// Adds a Service-Tag to an existing service
        /// </summary>
        /// <param name="serviceSession">the service-session for which to add a tag</param>
        /// <param name="tagName">the tag-key of the given value</param>
        /// <param name="value">the tag-value</param>
        public void AddServiceTag(ServiceSessionOperationMessage serviceSession, string tagName, string value)
        {
            if (!IsRemoteMessage(serviceSession, out var remoteBroker))
            {
                if (GrabService(serviceSession.ServiceName, serviceSession.SessionTicket, true, out var service,
                        out var operations, out var waitingMessages))
                {
                    service.SetTag(tagName, value);
                }
            }
            else
            {
                remoteBroker.AddServiceTag(serviceSession, tagName, value);
            }
        }

        /// <summary>
        /// Gets the ServiceName by its tag
        /// </summary>
        /// <param name="tagName">the tag-name for search</param>
        /// <param name="tagValue">the value</param>
        /// <returns>the first service that applies to the given filter</returns>
        public string GetServiceByTag(string tagName, string tagValue)
        {
            var svc = services.FirstOrDefault(n => n.Value.GetTag(tagName) == tagValue);
            return svc.Key;
        }

        public Task<ServerOperationMessage> NextRequest(ServiceSessionOperationMessage serviceSession)
        {
            if (!IsRemoteMessage(serviceSession, out var remoteBroker))
            {
                if (GrabService(serviceSession.ServiceName, serviceSession.SessionTicket, true, out var service,
                        out var operations, out var waitingMessages))
                {
                    if (service.ServiceKind == ServiceStatus.ServiceType.Local)
                    {
                        throw new CommunicationException("Not used for local clients!");
                    }

                    Task<OperationWaitHandle> retTask = null;
                    lock (service)
                    {
                        if (operations.IsEmpty && service.OpenTaskWait == null)
                        {
                            retTask = (service.OpenTaskWait =
                                new TaskCompletionSource<OperationWaitHandle>(TaskCreationOptions
                                    .RunContinuationsAsynchronously)).Task;
                        }
                        else if (!operations.IsEmpty && operations.TryDequeue(out var retVal))
                        {
                            retTask = Task.FromResult(retVal);
                        }
                    }

                    if (retTask != null)
                    {
                        return retTask.ContinueWith((t, s) =>
                        {
                            if (t.Result != null && !t.Result.ClientRequest.TickBack)
                            {
                                waitingMessages.TryAdd(t.Result.ClientRequest.OperationId, t.Result);
                            }

                            return t.Result?.ClientRequest;
                        }, null, TaskContinuationOptions.None);
                    }

                    //return retTask;
                }

                throw new CommunicationException("Unknown Service-grab - Error");
            }

            return remoteBroker.NextRequest(serviceSession);
            
        }

        public void FailOperation(ServiceSessionOperationMessage serviceSession, string req, Exception ex)
        {
            if (!IsRemoteMessage(serviceSession, out var remoteBroker))
            {
                if (req != null)
                {
                    try
                    {
                        if (GrabService(serviceSession.ServiceName, serviceSession.SessionTicket, true, out var service,
                                out var operations, out var waitingMessages))
                        {
                            if (waitingMessages.TryGetValue(req, out var rq))
                            {
                                rq.ServerResponse.SetException(ex);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        LogEnvironment.LogEvent($"Failed to set ServerResponse: {x.Message}", LogSeverity.Error);
                    }
                }
            }
            else
            {
                remoteBroker.FailOperation(serviceSession, req, ex);
            }
        }

        public void CommitServerOperation(ServiceOperationResponseMessage response)
        {
            if (!IsRemoteMessage(response, out var remoteBroker))
            {
                if (GrabService(response.TargetService, null, true, out var service, out var operations,
                        out var waitingMessages))
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
            else
            {
                remoteBroker.CommitServerOperation(response);
            }
        }

        public RegisterServiceResponseMessage RegisterService(RegisterServiceMessage registration)
        {
            if (!IsRemoteMessage(registration, out var remoteBroker))
            {
                lock (registrationLock)
                {
                    try
                    {
                        if (GrabService(registration.ServiceName, null, false, out var service, out var operations,
                                out var waitingMessages))
                        {
                            return new RegisterServiceResponseMessage
                            {
                                Reason = "Service is already registered!",
                                Ok = false
                            };
                        }

                        var newSvc = new ServiceStatus
                        {
                            LastPing = DateTime.Now,
                            Ttl = registration.Ttl,
                            ServiceName = registration.ServiceName,
                            RegistrationTicket =
                                $"{registration.ServiceName}_{DateTime.Now.Ticks}_{rnd.Next(10000000)}",
                            ServiceKind = ServiceStatus.ServiceType.InterProcess
                        };
                        StringBuilder fmsg = new StringBuilder();
                        bool ok = services.TryAdd(registration.ServiceName, newSvc);
                        if (!ok)
                        {
                            fmsg.Append(@"Service-Entry could not be added.
");
                        }

                        ok = ok && openWaitHandles.TryAdd(registration.ServiceName,
                            new ConcurrentDictionary<string, OperationWaitHandle>());
                        if (!ok)
                        {
                            fmsg.Append(@"OpenWait-Entry could not be added.
");
                        }

                        ok = ok && messages.TryAdd(registration.ServiceName,
                            new ConcurrentQueue<OperationWaitHandle>());
                        if (!ok)
                        {
                            fmsg.Append(@"OpenMsg-Entry could not be added.
");
                        }

                        var retVal = new RegisterServiceResponseMessage
                        {
                            SessionTicket = newSvc.RegistrationTicket,
                            Ok = ok,
                            Reason = ok ? "" : fmsg.ToString()
                        };
                        if (!ok)
                        {
                            UnsafeServerDrop(registration.ServiceName);
                        }

                        return retVal;
                    }
                    catch (Exception ex)
                    {
                        return new RegisterServiceResponseMessage
                        {
                            Ok = false,
                            Reason = ex.Message
                        };
                    }
                }
            }

            return remoteBroker.RegisterService(registration);
        }

        internal string RegisterService(ILocalServiceClient localClient)
        {
            if (!IsRemoteMessage(localClient.ServiceName, out var remoteBroker))
            {
                lock (registrationLock)
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
                            LogEnvironment.LogEvent(
                                $@"Replacing old Service Registration (serviceName: {o.ServiceName}, isAlive:{o.IsAlive}, lastPing:{o.LastPing:dd.MM.yyyy HH:mm:ss}, ticket:{o.RegistrationTicket}, serviceKind:{o.ServiceKind}, Ttl:{o.Ttl})
with new Registration (serviceName: {newSvc.ServiceName}, isAlive:{newSvc.IsAlive}, lastPing:{newSvc.LastPing:dd.MM.yyyy HH:mm:ss}, ticket:{newSvc.RegistrationTicket}, serviceKind:{newSvc.ServiceKind}, Ttl:{newSvc.Ttl})",
                                LogSeverity.Warning);
                            return newSvc;
                        });
                    return svc.RegistrationTicket;
                }
            }

            return remoteBroker.RegisterService(new RegisterServiceMessage
            {
                ResponderFor = localClient.ConsumedService,
                ServiceName = localClient.ServiceName,
                Ttl = 15
            }).SessionTicket;
        }

        public bool TryUnRegisterService(ServiceSessionOperationMessage msg)
        {
            if (!IsRemoteMessage(msg, out var remoteBroker))
            {
                bool retVal = true;
                try
                {
                    UnRegisterService(msg);
                }
                catch
                {
                    retVal = false;
                }

                return retVal;
            }

            return remoteBroker.TryUnRegisterService(msg);
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
            if (!IsRemoteMessage(request, out var remoteBroker))
            {
                LogEnvironment.LogDebugEvent($"Submitted Ticket: {request.SessionTicket}", LogSeverity.Report);
                if (GrabService(request.ServiceName, request.SessionTicket, true, out var service, out var operations,
                        out var waitingMessages))
                {
                    if (service.ServiceKind == ServiceStatus.ServiceType.Local)
                    {
                        throw new CommunicationException("Use the overload for local services!");
                    }

                    lock (service)
                    {
                        LogEnvironment.LogDebugEvent($"Ticket: {service.RegistrationTicket}", LogSeverity.Report);
                        service.LastPing = DateTime.Now;
                        var hnd = new OperationWaitHandle(new ServerOperationMessage { TickBack = true });
                        if (service.OpenTaskWait != null)
                        {
                            var t = service.OpenTaskWait;
                            service.OpenTaskWait = null;
                            t.SetResult(hnd);
                        }
                        else
                        {
                            operations.Enqueue(hnd);
                        }
                    }

                    return new ServiceTickResponseMessage
                    {
                        Ok = true,
                        PendingOperationsCount = operations.Count
                    };
                }

                throw new CommunicationException("Unknown Service-grab - Error");
            }

            return remoteBroker.Tick(request);
        }

        public ServiceDiscoverResponseMessage DiscoverService(ServiceDiscoverMessage request)
        {
            if (!IsRemoteMessage(request, out var remoteBroker))
            {
                var retVal = new ServiceDiscoverResponseMessage
                {
                    TargetService = request.TargetService,
                    Ok = false,
                    Reason = "Service is not available",
                };
                if (GrabService(request.TargetService, null, false, out var service, out var operations,
                        out var waitingMessages))
                {
                    retVal.Reason = string.Empty;
                    retVal.Ok = true;
                }

                return retVal;
            }

            return remoteBroker.DiscoverService(request);
        }

        public void Dispose()
        {
            tickOpenWaits.Dispose();
        }

        public void UnsafeServerDrop(string serviceName)
        {
            if (!IsRemoteMessage(serviceName, out var remoteBroker))
            {
                lock (registrationLock)
                {
                    if (services.TryRemove(serviceName, out var svc))
                    {
                        lock (svc)
                        {
                            if (svc.OpenTaskWait != null)
                            {
                                var t = svc.OpenTaskWait;
                                svc.OpenTaskWait = null;
                                t.SetResult(null);
                            }
                        }
                    }

                    var hasOps = messages.TryRemove(serviceName, out var operations);
                    var hasMsg = openWaitHandles.TryRemove(serviceName, out var waitingMessages);
                    if (hasMsg)
                    {
                        foreach (var openWaitHandle in waitingMessages)
                        {
                            if (!openWaitHandle.Value.ServerResponse.Task.IsCompleted)
                            {
                                openWaitHandle.Value.ServerResponse.SetException(
                                    new Exception("Service is shutting down."));
                            }
                        }
                    }

                    if (hasOps)
                    {
                        while (operations.TryDequeue(out var msg))
                        {
                            if (!msg.ServerResponse.Task.IsCompleted)
                            {
                                msg.ServerResponse.SetException(new Exception("Service is shutting down."));
                            }
                        }
                    }
                }
            }
            else
            {
                remoteBroker.UnsafeServerDrop(serviceName);
            }
        }

        private void UnRegisterService(ServiceSessionOperationMessage request)
        {
            if (GrabService(request.ServiceName, request.SessionTicket, true, out var service, out var operations, out var waitingMessages))
            {
                UnsafeServerDrop(request.ServiceName);
            }
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

                throw new ServiceUnknownException("Service is unknown!");
            }

            if (serviceStatus.ServiceKind == ServiceStatus.ServiceType.Local)
            {
                pendingOps = null;
                waitingMessages = null;
                return true;
            }

            if (!serviceStatus.IsAlive)
            {
                UnsafeServerDrop(serviceStatus.ServiceName);
                if (!throwWhenInvalid)
                {
                    pendingOps = null;
                    waitingMessages = null;
                    return false;
                }

                throw new ServiceTimeoutException("Service-connection has timed out!");
            }

            if (!string.IsNullOrEmpty(serviceTicket) && serviceTicket != serviceStatus.RegistrationTicket)
            {
                UnsafeServerDrop(serviceStatus.ServiceName);
                if (!throwWhenInvalid)
                {
                    pendingOps = null;
                    waitingMessages = null;
                    return false;
                }

                throw new ServiceBrokenException("Session-Ticket is invalid!");
            }

            if (!openWaitHandles.TryGetValue(serviceName, out waitingMessages))
            {
                UnsafeServerDrop(serviceStatus.ServiceName);
                throw new ServiceBrokenException("Service is broken!");
            }

            if (!messages.TryGetValue(serviceName, out pendingOps))
            {
                UnsafeServerDrop(serviceStatus.ServiceName);
                throw new ServiceBrokenException("Service is broken!");
            }

            return true;
        }

        private void TickOpenWaits(object state)
        {
            foreach (var s in services)
            {
                lock (s.Value)
                {
                    if (!s.Value.IsAlive && s.Value.OpenTaskWait != null)
                    {
                        var t = s.Value.OpenTaskWait;
                        s.Value.OpenTaskWait = null;
                        t.SetResult(null);
                    }
                    else if (s.Value.IsAlive && messages.TryGetValue(s.Key, out var q) && !q.IsEmpty && s.Value.OpenTaskWait != null && q.TryDequeue(out var m))
                    {
                        LogEnvironment.LogEvent("Unexpected Behavior in OpenTickWaits!", LogSeverity.Warning);
                        var t = s.Value.OpenTaskWait;
                        s.Value.OpenTaskWait = null;
                        t.SetResult(m);
                    }
                }
            }
        }

        public void RegisterBrokerProxy(string name, IEndPointBroker subBroker)
        {
            children.AddOrUpdate(name, subBroker, (s, o) => subBroker);
        }

        private bool IsRemoteMessage(IServiceMessage message, out IEndPointBroker proxy)
        {
            proxy = null;
            if (message.TargetService.Contains("@"))
            {
                return IsRemoteMessage(message.TargetService, out proxy);
            }

            return false;
        }

        private bool IsRemoteMessage(IServerMessage message, out IEndPointBroker proxy)
        {
            proxy = null;
            if (message.ServiceName.Contains("@"))
            {
                return IsRemoteMessage(message.ServiceName, out proxy);
            }

            return false;
        }

        private bool IsRemoteMessage(string serviceName, out IEndPointBroker proxy)
        {
            var t = serviceName.LastIndexOf("@") + 1;
            var svc = serviceName.Substring(t);
            if (children.TryGetValue(svc, out proxy))
            {
                return true;
            }

            return false;
        }
    }
}