using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ITVComponents.GenericService.ServiceSecurity;
using ITVComponents.GenericService.WebService;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.Grpc.Extensions;
using ITVComponents.InterProcessCommunication.Grpc.Hub.Protos;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace ITVComponents.InterProcessCommunication.Grpc.Hub.Hubs
{
    [Authorize]
    internal class AuthServiceHubRpc : OpenServiceHubRpc
    {
        public AuthServiceHubRpc(IServiceHubProvider serviceBackend) : base(serviceBackend)
        {
        }

        public override Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request, ServerCallContext context)
        {
            try
            {
                CheckAuth(context, "ConnectAnyService", request.TargetService);
                request.HubUser = JsonHelper.ToJsonStrongTyped(((ClaimsIdentity)context.GetHttpContext().User.Identity).ForTransfer());
                return base.ConsumeService(request, context);
            }
            catch (Exception ex)
            {
                return Task.FromException<ServiceOperationResponseMessage>(ex);
            }
        }

        public override Task<ServiceDiscoverResponseMessage> DiscoverService(ServiceDiscoverMessage request, ServerCallContext context)
        {
            try
            {
                CheckAuth(context, "ConnectAnyService", request.TargetService);
                return base.DiscoverService(request, context);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ServiceDiscoverResponseMessage { Ok = false, Reason = ex.Message, TargetService = request.TargetService });
            }
        }

        public override Task<RegisterServiceResponseMessage> RegisterService(RegisterServiceMessage request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ResponderFor))
                {
                    CheckAuth(context, "ActAsService");
                    TemporaryGrants.RegisterService(request.ServiceName, context.GetHttpContext().User.Identity.Name);
                }
                else
                {
                    CheckAuth(context, "ConnectAnyService", request.ResponderFor);
                    TemporaryGrants.GrantTemporaryPermission(request.ResponderFor, request.ServiceName);
                }

                return base.RegisterService(request, context);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                return Task.FromResult(new RegisterServiceResponseMessage { Ok = false, Reason = ex.Message });
            }
        }

        public override async Task ServiceReady(ServiceSessionOperationMessage request, IServerStreamWriter<ServerOperationMessage> responseStream, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ResponderFor))
                {
                    CheckAuth(context, "ActAsService");
                }
                else
                {
                    CheckAuth(context, "ConnectAnyService", request.ResponderFor);
                }

                await base.ServiceReady(request, responseStream, context);
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent(ex.OutlineException(), LogSeverity.Error);
                throw;
            }
            finally
            {
                TemporaryGrants.UnRegisterService(request.ServiceName);
                if (!string.IsNullOrEmpty(request.ResponderFor))
                {
                    TemporaryGrants.RevokeTemporaryPermission(request.ResponderFor, request.ServiceName);
                }
            }
        }

        public override Task<ServiceTickResponseMessage> ServiceTick(ServiceSessionOperationMessage request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ResponderFor))
                {
                    CheckAuth(context, "ActAsService");
                }
                else
                {
                    CheckAuth(context, "ConnectAnyService", request.ResponderFor);
                }

                return base.ServiceTick(request, context);
            }
            catch (Exception ex)
            {
                return Task.FromException<ServiceTickResponseMessage>(ex);
            }
        }

        public override Task<Empty> CommitServiceOperation(ServiceOperationResponseMessage request, ServerCallContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ResponderFor))
                {
                    CheckAuth(context, "ActAsService");
                }
                else
                {
                    CheckAuth(context, "ConnectAnyService", request.ResponderFor);
                }

                return base.CommitServiceOperation(request, context);
            }
            catch (Exception ex)
            {
                return Task.FromException<Empty>(ex);
            }
        }

        private void CheckAuth(ServerCallContext context, params string[] requiredPermissions)
        {
            if (!context.GetHttpContext().RequestServices.VerifyUserPermissions(requiredPermissions))
            {
                throw new SecurityException($"Access denied for the following permissions: {string.Join(",", requiredPermissions)}");
            }
        }
    }
}
