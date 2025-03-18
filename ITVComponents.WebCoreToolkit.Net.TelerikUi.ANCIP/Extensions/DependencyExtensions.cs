using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Services.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Extensions
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
