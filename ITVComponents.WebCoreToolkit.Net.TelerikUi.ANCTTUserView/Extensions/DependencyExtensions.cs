using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTreeTenants.Model;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTreeTenantSecurityUserView.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection UseSecurityContextUserExtensions(this IServiceCollection services)
        {
            return services
                .AddSingleton<IUserExpressionHelper<string, User, HierarchyTenantUser, RoleRole>, TenantSecurityUserExpressionHelper>();
        }
    }
}
