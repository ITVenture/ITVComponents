using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Hubs
{
    internal class OpenServiceHubRpc:ServiceHub.ServiceHubBase
    {
        private readonly IServiceHubProvider serviceBackend;

        public OpenServiceHubRpc(IServiceHubProvider serviceBackend)
        {
            this.serviceBackend = serviceBackend;
        }

        public override Task<Empty> CommitServiceOperation(ServiceOperationResponseMessage request, ServerCallContext context)
        {
            serviceBackend.Broker.CommitServerOperation(request);
            return Task.FromResult(new Empty());
        }

        public override async Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request, ServerCallContext context)
        {
            //context.GetHttpContext().RequestServices.VerifyUserPermissions()
            return await serviceBackend.Broker.SendMessageToServer(request);
        }

        public override Task<ServiceDiscoverResponseMessage> DiscoverService(ServiceDiscoverMessage request, ServerCallContext context)
        {
            return Task.FromResult(serviceBackend.Broker.DiscoverService(request));
        }

        public override Task<RegisterServiceResponseMessage> RegisterService(RegisterServiceMessage request, ServerCallContext context)
        {
            return Task.FromResult(serviceBackend.Broker.RegisterService(request));
        }

        public override async Task ServiceReady(ServiceSessionOperationMessage request, IServerStreamWriter<ServerOperationMessage> responseStream, ServerCallContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            try
            {
                do
                {
                    try
                    {
                        var nextMessage = serviceBackend.Broker.NextRequest(request);
                        if (nextMessage != null)
                        {
                            await responseStream.WriteAsync(nextMessage).ConfigureAwait(false);
                        }
                        else
                        {
                            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Error occurred: {ex.OutlineException()}", LogSeverity.Error);
                    }

                } while (cancellationToken.IsCancellationRequested == false);
            }
            finally
            {
                serviceBackend.Broker.UnRegisterService(request);
            }
        }

        public override Task<ServiceTickResponseMessage> ServiceTick(ServiceSessionOperationMessage request, ServerCallContext context)
        {
            return Task.FromResult(serviceBackend.Broker.Tick(request));
        }
    }
}
