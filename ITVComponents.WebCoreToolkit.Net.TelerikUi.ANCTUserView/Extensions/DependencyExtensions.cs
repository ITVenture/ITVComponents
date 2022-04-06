using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Helpers;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection UseSecurityContextUserExtensions(this IServiceCollection services)
        {
            return services
                .AddSingleton<IUserExpressionHelper<string, User, TenantUser>, TenantSecurityUserExpressionHelper>();
        }
    }
}
