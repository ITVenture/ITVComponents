using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Extensions
{
    public static class DependencyExtensions
    {
        public static IServiceCollection ConfigureIdentityPages(this IServiceCollection services,
            Action<ManageNavOptions> configure)
        {
            return services.Configure(configure);
        }
    }
}
