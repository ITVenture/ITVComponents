using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Channels;
using ITVComponents.InterProcessCommunication.InMemory.Hub.Communication;
using ITVComponents.InterProcessCommunication.MessagingShared.Extensions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Exceptions;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.HubSecurity;
using ITVComponents.InterProcessCommunication.MessagingShared.Hub.Protocol;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.InterProcessCommunication.InMemory.Hub.Hubs
{
    internal class AuthServiceHubIm : OpenServiceHubIm
    {
        private readonly IUserNameMapper userMapper;
        private readonly ISecurityRepository securityRepo;

        public AuthServiceHubIm(IServiceHubProvider serviceBackend, IUserNameMapper userMapper, ISecurityRepository securityRepo) :base(serviceBackend)
        {
            this.userMapper = userMapper;
            this.securityRepo = securityRepo;
        }

        public override Task<ServiceOperationResponseMessage> ConsumeService(ServerOperationMessage request, DataTransferContext context)
        {
            try
            {
                CheckAuth(context, "ConnectAnyService", request.TargetService);
                request.HubUser = JsonHelper.ToJsonStrongTyped(((ClaimsIdentity)context.Identity).ForTransfer());
                return base.ConsumeService(request, context);
            }
            catch (Exception ex)
            {
                return Task.FromException<ServiceOperationResponseMessage>(ex);
            }
        }

        public override Task<ServiceDiscoverResponseMessage> DiscoverService(ServiceDiscoverMessage request, DataTransferContext context)
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

        public override Task<RegisterServiceResponseMessage> RegisterService(RegisterServiceMessage request, DataTransferContext context)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ResponderFor))
                {
                    CheckAuth(context, "ActAsService");
                    TemporaryGrants.RegisterService(request.ServiceName, context.Identity.Name);
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

        public override async Task ServiceReady(ServiceSessionOperationMessage request, IMemoryChannel channel, DataTransferContext context)
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

                await base.ServiceReady(request, channel, context);
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

        public override Task<ServiceTickResponseMessage> ServiceTick(ServiceSessionOperationMessage request, DataTransferContext context)
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

        public override Task CommitServiceOperation(ServiceOperationResponseMessage request, DataTransferContext context)
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
                return Task.FromException(ex);
            }
        }

        private void CheckAuth(DataTransferContext context, params string[] requiredPermissions)
        {
            if (!VerifyUserPermissions(requiredPermissions, context))
            {
                throw new SecurityException($"Access denied for the following permissions: {string.Join(",", requiredPermissions)}");
            }
        }

        private bool VerifyUserPermissions(string[] requiredPermissions, DataTransferContext context)
        {
            var labels = userMapper.GetUserLabels(context.Identity);
            var permissions = securityRepo.GetPermissions(labels, ((ClaimsIdentity)context.Identity).AuthenticationType).Select(n => n.PermissionName).Distinct().ToArray();
            return requiredPermissions.Length == 0 || requiredPermissions.Any(t => permissions.Contains(t, StringComparer.OrdinalIgnoreCase));

        }
    }
}
