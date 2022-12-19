using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using ITVComponents.WebCoreToolkit.Net.ViewModel;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Hubs
{
    [Authorize]

    public class GlobalNotificationHub:Hub
    {
        private static ConcurrentDictionary<string, ConcurrentDictionary<string, string>> userMap = new ConcurrentDictionary<string, ConcurrentDictionary<string,string>>();
        private readonly ISecurityRepository securityRepo;
        private readonly IServiceProvider services;
        private readonly IPermissionScope currentScope;
        private readonly IUserNameMapper userNameMapper;

        public GlobalNotificationHub(ISecurityRepository securityRepo, IServiceProvider services, IPermissionScope currentScope, IUserNameMapper userNameMapper)
        {
            this.securityRepo = securityRepo;
            this.services = services;
            this.currentScope = currentScope;
            this.userNameMapper = userNameMapper;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            await Groups.AddToGroupAsync(Context.ConnectionId, currentScope.PermissionPrefix);
            var labels = userNameMapper.GetUserLabels(Context.User.Identity);
            var allRoles = (from u in securityRepo.Users
                join l in labels on u.UserName equals l
                select securityRepo.GetRoles(u)).SelectMany(r => r).Select(r => r.RoleName).Distinct();
            foreach (var role in allRoles)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"{currentScope.PermissionPrefix}.{role}");
            }

            MapUserToId(Context.User.Identity.Name, Context.UserIdentifier);
        }

        public static async Task NotifyAsync(IServiceProvider services, NotificationModel notificationDefinition)
        {

            var docHub = services.GetService<IHubContext<GlobalNotificationHub>>();
            if (docHub != null)
            {
                IClientProxy proxy = null;
                string[] permissionGroups = null;
                if (notificationDefinition.UsersWithPermissions != null &&
                    notificationDefinition.UsersWithPermissions.Length != 0 && !string.IsNullOrEmpty(notificationDefinition.TenantName))
                {
                    ISecurityRepository srp = services.GetService<ISecurityRepository>();
                    permissionGroups = srp.GetRolesWithPermissions(notificationDefinition.UsersWithPermissions, notificationDefinition.TenantName).Select(n => n.RoleName).ToArray();
                }

                if (!string.IsNullOrEmpty(notificationDefinition.UserName))
                {
                    proxy = docHub.Clients.Users(UserIds(notificationDefinition.UserName));
                }
                else if (!string.IsNullOrEmpty(notificationDefinition.RoleName) &&
                         !string.IsNullOrEmpty(notificationDefinition.TenantName))
                {
                    proxy = docHub.Clients.Group(
                        $"{notificationDefinition.TenantName}.{notificationDefinition.RoleName}");
                }
                else if (!string.IsNullOrEmpty(notificationDefinition.TenantName))
                {
                    if (permissionGroups == null)
                    {
                        proxy = docHub.Clients.Group(notificationDefinition.TenantName);
                    }
                    else
                    {
                        proxy = docHub.Clients.Groups(from t in permissionGroups
                            select $"{notificationDefinition.TenantName}.{t}");
                    }
                }
                else if (notificationDefinition.Broadcast)
                {
                    proxy = docHub.Clients.All;
                }

                if (proxy != null)
                {
                    await proxy.SendCoreAsync("Notify", new object[]
                    {
                        notificationDefinition.Url,
                        notificationDefinition.Topic,
                        notificationDefinition.Data
                    });
                }
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);
            UnmapUser(Context.User.Identity.Name, Context.UserIdentifier);
        }

        private static void MapUserToId(string userName, string userId)
        {
            var userDc = userMap.GetOrAdd(userName, u => new ConcurrentDictionary<string, string>());
            userDc.AddOrUpdate(userId, k => userName, (u, k) => userName);
        }

        private static void UnmapUser(string userName, string userId)
        {
            var userDc = userMap.GetOrAdd(userName, u => new ConcurrentDictionary<string, string>());
            if (userDc != null)
            {
                userDc.TryRemove(userId, out _);
                if (userDc.IsEmpty)
                {
                    userMap.TryRemove(userName, out _);
                }
            }
        }

        private static string[] UserIds(string userName)
        {
            if (userMap.TryGetValue(userName, out var userDc))
            {
                return userDc.Keys.ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
