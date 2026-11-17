using System;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace ITVComponents.GenericService.Windows
{
    /// <summary>
    /// Runs a generic service as a Windows service.
    /// </summary>
    public static class GenericServiceHostExtensions
    {
        /// <summary>
        /// Like <c>UseWindowsService()</c>, and the service additionally receives the Windows session notifications
        /// (<see cref="ITVComponents.GenericService.Sessions.IServiceSessionEvents"/>). Outside a Windows service (console,
        /// debugging) it changes nothing.
        /// </summary>
        /// <param name="builder">the host builder handed to <see cref="Service.Run"/></param>
        /// <returns>the same builder</returns>
        /// <example><c>Service.Run(args, h => h.UseGenericWindowsService());</c></example>
        /// <remarks>
        /// A service that must also run on Linux keeps its host neutral and calls this only on Windows
        /// (<c>if (OperatingSystem.IsWindows())</c>), or references this package only in its Windows build.
        /// </remarks>
        [SupportedOSPlatform("windows")]
        public static IHostBuilder UseGenericWindowsService(this IHostBuilder builder)
        {
            builder.UseWindowsService();
            if (!WindowsServiceHelpers.IsWindowsService())
            {
                return builder;
            }

            return builder.ConfigureServices((_, services) =>
            {
                var existing = services.Where(d => d.ServiceType == typeof(IHostLifetime)).ToArray();
                foreach (var descriptor in existing)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<IHostLifetime, SessionAwareServiceLifetime>();
            });
        }
    }
}
