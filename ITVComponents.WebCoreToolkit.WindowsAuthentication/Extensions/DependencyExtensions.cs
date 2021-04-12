using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.ClaimsTransformation;
using ITVComponents.WebCoreToolkit.WindowsAuthentication.Options;
using ITVComponents.WebCoreToolkit.WindowsAuthentication.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.WindowsAuthentication.Extensions
{
    public static class DependencyExtensions
    {
        /// <summary>
        /// Automatically translates Windows-User-Group SID to its readable name
        /// </summary>
        /// <param name="services">the services where transformation-provider is injected</param>
        /// <param name="collectable">indicates whether to inject the RepositoryClaims transformer in a way, that enables the usage of multiple transformers</param>
        public static IServiceCollection AutoTranslateWindowsGroupSid(this IServiceCollection services, bool collectable = false)
        {

            return AutoTranslateWindowsGroupSid(services, n => { }, collectable);
        }
        
        
        /// <summary>
        /// Automatically translates Windows-User-Group SID to its readable name
        /// </summary>
        /// <param name="services">the services where transformation-provider is injected</param>
        /// <param name="collectable">indicates whether to inject the RepositoryClaims transformer in a way, that enables the usage of multiple transformers</param>
        public static IServiceCollection AutoTranslateWindowsGroupSid(this IServiceCollection services, Action<RolesTransformationOptions> options, bool collectable = false)
        {
            services.Configure(options);
            if (!collectable)
            {
                return services.AddScoped<IClaimsTransformation, WindowsRolesSidTransform>();
            }

            return services.AddScoped<ICollectedClaimsProvider, WindowsRolesSidTransform>();
        }
    }
}
