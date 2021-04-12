using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Security;
using ITVComponents.InterProcessCommunication.Grpc.Security.PrincipalProviders;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.InterProcessCommunication.Grpc.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection IPCUserFromHttpContext(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<IIdentityProvider, IdentityFromHttpContextProvider>();
            return services;
        } 
    }
}
