using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityContextUserView.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection UseSecurityContextUserExtensions(this IServiceCollection services)
        {
            return services
                .AddSingleton<IUserExpressionHelper<int, User, TenantUser, RoleRole>, TenantSecurityUserExpressionHelper>();
        }
    }
}
