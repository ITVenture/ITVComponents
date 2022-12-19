using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.Hubs;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Extensions
{
    public static class ServicesExtensions
    {
        public static async Task NotifyFrontendAsync(this IServiceProvider services, NotificationModel notification)
        {
            await GlobalNotificationHub.NotifyAsync(services, notification);
        }

        public static async Task NotifyDataChangeAsync(this IServiceProvider services, string entryPoint, string tenantName, string roleName, string targetGrid)
        {
            await NotifyFrontendAsync(services, new NotificationModel
            {
                Topic = "ModuleData",
                TenantName = tenantName,
                RoleName = roleName,
                Url = entryPoint,
                Data = new Dictionary<string, object>
                {
                    { "listName", targetGrid }
                }
            });
        }

        public static async Task NotifyDataChangeAsync(this IServiceProvider services, string entryPoint, string tenantName, string targetGrid, string[] requiredPermissions)
        {
            await NotifyFrontendAsync(services, new NotificationModel
            {
                Topic = "ModuleData",
                TenantName = tenantName,
                UsersWithPermissions=requiredPermissions,
                Url = entryPoint,
                Data = new Dictionary<string, object>
                {
                    { "listName", targetGrid }
                }
            });
        }
    }
}
