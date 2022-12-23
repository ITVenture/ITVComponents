using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Hubs
{
    internal class OpenServiceHubRpc : ServiceHub.ServiceHubBase
    {
        private readonly IServiceHubProvider serviceBackend;

        public OpenServiceHubRpc(IServiceHubProvider serviceBackend)
        {
            this.serviceBackend = serviceBackend;
        }


        public override Task<Empty> CommitServiceOperation(ServiceOperationResponseMessage request, ServerCallContext context)
        {
            serviceBackend.Broker.CommitServerOperation(new MessagingShared.Hub.Protocol.ServiceOperationResponseMessage
            {
                Ok = request.Ok,
                OperationId = request.OperationId,
                ResponderFor = request.ResponderFor,
                ResponsePayload = request.ResponsePayload,
                TargetService = request.TargetService
            });
            return Task.FromResult(new Empty());
        }

        public override async Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request, ServerCallContext context)
        {
            //context.GetHttpContext().RequestServices.VerifyUserPermissions()
            
            var retRaw = await serviceBackend.Broker.SendMessageToServer(new MessagingShared.Hub.Protocol.ServerOperationMessage
            {
                OperationId = request.OperationId,
                TargetService = request.TargetService,
                HubUser = request.HubUser,
                OperationPayload = request.OperationPayload,
                TickBack = request.TickBack
                
            }, context.GetHttpContext().RequestServices);
            return new ServiceOperationResponseMessage
            {
                OperationId = retRaw.OperationId ?? "",
                TargetService = retRaw.TargetService ?? "",
                Ok = retRaw.Ok,
                ResponderFor = retRaw.ResponderFor ?? "",
                ResponsePayload = retRaw.ResponsePayload ?? ""
            };
        }

        public override Task<ServiceDiscoverResponseMessage> DiscoverService(ServiceDiscoverMessage request, ServerCallContext context)
        {
            var retRaw = serviceBackend.Broker.DiscoverService(new MessagingShared.Hub.Protocol.ServiceDiscoverMessage
            {
                TargetService = request.TargetService
            });

            return Task.FromResult(new ServiceDiscoverResponseMessage
            {
                Ok = retRaw.Ok,
                TargetService = retRaw.TargetService ?? "",
                Reason = retRaw.Reason ?? ""
            });
        }

        public override Task<RegisterServiceResponseMessage> RegisterService(RegisterServiceMessage request, ServerCallContext context)
        {
            var retRaw = serviceBackend.Broker.RegisterService(new MessagingShared.Hub.Protocol.RegisterServiceMessage
            {
                ResponderFor = request.ResponderFor,
                ServiceName = request.ServiceName,
                Ttl = request.Ttl
            });
            var ret = new RegisterServiceResponseMessage
            {
                Ok = retRaw.Ok,
                Reason = retRaw.Reason ?? "",
                SessionTicket = retRaw.SessionTicket ?? ""
            };

            return Task.FromResult(ret);
        }

        public override async Task ServiceReady(ServiceSessionOperationMessage request, IServerStreamWriter<ServerOperationMessage> responseStream, ServerCallContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            var req = new MessagingShared.Hub.Protocol.ServiceSessionOperationMessage
            {
                ResponderFor = request.ResponderFor ?? "",
                SessionTicket = request.SessionTicket ?? "",
                Ttl = request.Ttl,
                ServiceName = request.ServiceName ?? "",
                Tick = false
            };
            try
            {
                do
                {
                    try
                    {
                        var nextMessage = await serviceBackend.Broker.NextRequest(req);
                        if (nextMessage != null)
                        {
                            await responseStream.WriteAsync(new ServerOperationMessage
                            {
                                HubUser = nextMessage.HubUser ?? "",
                                OperationId = nextMessage.OperationId ?? "",
                                OperationPayload = nextMessage.OperationPayload ?? "",
                                TargetService = nextMessage.TargetService ?? "",
                                TickBack = nextMessage.TickBack
                            }).ConfigureAwait(false);
                        }
                        else
                        {
                            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (ServiceBrokenException sbe)
                    {
                        LogEnvironment.LogEvent($"Error occurred: {sbe.Message}", LogSeverity.Error);
                        break;
                    }
                    catch (ServiceTimeoutException ste)
                    {
                        LogEnvironment.LogEvent($"Error occurred: {ste.Message}", LogSeverity.Error);
                        break;
                    }
                    catch (ServiceUnknownException sue)
                    {
                        LogEnvironment.LogEvent($"Error occurred: {sue.Message}", LogSeverity.Error);
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Error occurred: {ex.OutlineException()}", LogSeverity.Error);
                    }

                } while (cancellationToken.IsCancellationRequested == false);
            }
            finally
            {
                serviceBackend.Broker.TryUnRegisterService(req);
            }
        }

        public override Task<ServiceTickResponseMessage> ServiceTick(ServiceSessionOperationMessage request, ServerCallContext context)
        {
            var retRaw = serviceBackend.Broker.Tick(new MessagingShared.Hub.Protocol.ServiceSessionOperationMessage
            {
                ResponderFor = request.ResponderFor,
                ServiceName = request.ServiceName,
                SessionTicket = request.SessionTicket,
                Ttl = request.Ttl,
                Tick = true
            });
            return Task.FromResult(new ServiceTickResponseMessage
            {
                Ok = retRaw.Ok,
                PendingOperationsCount = retRaw.PendingOperationsCount,
                Reason = retRaw.Reason ?? ""
            });
        }
    }
}
