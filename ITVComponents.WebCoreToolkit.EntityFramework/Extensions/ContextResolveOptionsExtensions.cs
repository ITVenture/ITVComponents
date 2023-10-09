using ITVComponents.WebCoreToolkit.EntityFramework.Options;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Extensions
{
    public static class ContextResolveOptionsExtensions
    {
        /// <summary>
        /// Registers a Factory that resolves Db-Contexts through the Plugin-Environment
        /// </summary>
        /// <param name="options">the foreign-key options object</param>
        /// <param name="usePermissionScope">indicates whether to use the permission-scope</param>
        /// <returns>the provided options-object for method-chaining</returns>
        public static T UsePlugins<T>(this T options, bool usePermissionScope = true) where T:ContextResolveOptions
        {
            return (T)options.RegisterService("*", (provider, s, area) =>
            {
                IWebPluginHelper plugins = provider.GetService<IWebPluginHelper>();
                IPermissionScope scope = null;
                if (usePermissionScope)
                {
                    scope = provider.GetService<IPermissionScope>();
                }

                var name = $"{scope?.PermissionPrefix}{area}{s}";
                var factory = plugins.GetFactory();
                var retVal = factory[name];
                if (retVal == null)
                {
                    name = $"{scope?.PermissionPrefix}{s}";
                    retVal = factory[name];
                }
                if (retVal == null)
                {
                    retVal = factory[s];
                }

                return retVal;
            });
        }
    }
}
