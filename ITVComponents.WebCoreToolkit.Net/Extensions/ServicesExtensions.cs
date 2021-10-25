using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.FileHandling;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.Net.Extensions
{
    public static class ServicesExtensions
    {
        /// <summary>
        /// Gets the FileHandler with a specific name
        /// </summary>
        /// <param name="services">the serviceprovider that holds injectable services</param>
        /// <param name="rawName">the expected raw-name of the file-handler</param>
        /// <returns>the requested instance when it was found.</returns>
        public static object GetFileHandler(this IServiceProvider services, string rawName)
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
