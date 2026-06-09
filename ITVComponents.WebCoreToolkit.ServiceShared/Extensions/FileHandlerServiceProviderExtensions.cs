using System;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.ServiceShared.FileHandling;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.ServiceShared.Extensions
{
    /// <summary>
    /// Host-neutral resolution of a FileHandler-plugin. Uses only the framework-neutral plugin-discovery
    /// (<see cref="IWebPluginHelper"/>) and the current permission-scope, so MVC and Blazor resolve handlers
    /// identically and in-process — no MVC endpoint required.
    /// </summary>
    public static class FileHandlerServiceProviderExtensions
    {
        /// <summary>
        /// Gets the FileHandler with a specific name. The current <see cref="IPermissionScope"/> prefix is honored
        /// first, falling back to the raw name.
        /// </summary>
        /// <param name="services">the service-provider that holds injectable services</param>
        /// <param name="rawName">the expected raw-name of the file-handler</param>
        /// <returns>the resolved <see cref="IAsyncFileHandler"/> or <see cref="IFileHandler"/>, or null when not found</returns>
        public static IFileReasonPermissionProvider GetFileHandler(this IServiceProvider services, string rawName)
        {
            IWebPluginHelper plugins = services.GetService<IWebPluginHelper>();
            IPermissionScope scope = services.GetService<IPermissionScope>();
            var name = $"{scope?.PermissionPrefix}{rawName}";
            var factory = plugins.GetFactory();
            var retVal = factory[name, true];
            if (retVal == null)
            {
                retVal = factory[rawName, true];
            }

            if (retVal is IAsyncFileHandler afh)
            {
                return afh;
            }

            if (retVal is IFileHandler sfh)
            {
                return sfh;
            }

            return null;
        }
    }
}
