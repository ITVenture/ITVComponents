using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Logging;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Hubs
{
    internal class OpenServiceHubIm:IServiceHub
    {
        private readonly IServiceHubProvider serviceBackend;

        public OpenServiceHubIm(IServiceHubProvider serviceBackend)
        {
            this.serviceBackend = serviceBackend;
        }

        public virtual Task CommitServiceOperation(ServiceOperationResponseMessage request, DataTransferContext context)
        {
            serviceBackend.Broker.CommitServerOperation(request);

            return Task.CompletedTask;
        }

        public virtual async Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request, DataTransferContext context)
        {
            //context.GetHttpContext().RequestServices.VerifyUserPermissions()
            var retRaw = await serviceBackend.Broker.SendMessageToServer(request);
            return retRaw;
        }

        public virtual Task<ServiceDiscoverResponseMessage> DiscoverService(ServiceDiscoverMessage request, DataTransferContext context)
        {
            var retRaw = serviceBackend.Broker.DiscoverService(request);

            return Task.FromResult(retRaw);
        }

        public virtual Task<RegisterServiceResponseMessage> RegisterService(RegisterServiceMessage request, DataTransferContext context)
        {
            LogEnvironment.LogDebugEvent("Registering Service...", LogSeverity.Report);
            var retRaw = serviceBackend.Broker.RegisterService(request);
            return Task.FromResult(retRaw);
        }

        public virtual async Task ServiceReady(ServiceSessionOperationMessage request, IMemoryChannel channel, DataTransferContext context)
        {
            CancellationToken cancellationToken = channel.CancellationToken;
            try
            {
                serviceBackend.Broker.AddServiceTag(request, "ChannelName",channel.Name);
                do
                {
                    string req = null;
                    try
                    {
                        var nextMessage = await serviceBackend.Broker.NextRequest(request);
                        req = nextMessage.OperationId;
                        if (nextMessage != null)
                        {
                            await channel.WriteAsync(new ServerOperationMessage
                            {
                                HubUser = nextMessage.HubUser,
                                OperationId = nextMessage.OperationId,
                                OperationPayload = nextMessage.OperationPayload,
                                TargetService = nextMessage.TargetService,
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
                        serviceBackend.Broker.FailOperation(request, req, sbe);
                        LogEnvironment.LogEvent($"Error occurred: {sbe.Message}", LogSeverity.Error);
                        break;
                    }
                    catch (ServiceTimeoutException ste)
                    {
                        serviceBackend.Broker.FailOperation(request, req, ste);
                        LogEnvironment.LogEvent($"Error occurred: {ste.Message}", LogSeverity.Error);
                        break;
                    }
                    catch (ServiceUnknownException sue)
                    {
                        serviceBackend.Broker.FailOperation(request, req, sue);
                        LogEnvironment.LogEvent($"Error occurred: {sue.Message}", LogSeverity.Error);
                        break;
                    }
                    catch (Exception ex)
                    {
                        serviceBackend.Broker.FailOperation(request, req, ex);
                        LogEnvironment.LogEvent($"Error occurred: {ex.OutlineException()}", LogSeverity.Error);
                    }

                } while (cancellationToken.IsCancellationRequested == false);
            }
            finally
            {
                serviceBackend.Broker.TryUnRegisterService(request);
            }
        }

        public virtual Task<ServiceTickResponseMessage> ServiceTick(ServiceSessionOperationMessage request, DataTransferContext context)
        {
            var retRaw = serviceBackend.Broker.Tick(request);
            return Task.FromResult(new ServiceTickResponseMessage
            {
                Ok = retRaw.Ok,
                PendingOperationsCount = retRaw.PendingOperationsCount,
                Reason = retRaw.Reason
            });
        }
    }
}
