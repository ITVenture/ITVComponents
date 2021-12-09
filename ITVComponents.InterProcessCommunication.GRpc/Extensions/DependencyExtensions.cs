using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.Grpc.Security.PrincipalProviders;
using ITVComponents.InterProcessCommunication.MessagingShared.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection IPCUserFromHttpContext(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IIdentityProvider, IdentityFromHttpContextProvider>();
            return services;
        } 
    }
}
