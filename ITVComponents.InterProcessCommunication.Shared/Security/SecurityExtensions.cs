using System;
using ITVComponents.Plugins;

namespace ITVComponents.InterProcessCommunication.Shared.Security
{
    public static class SecurityExtensions
    {
        /// <summary>
        /// Checks on an attribute whether it requires a secure channel in order to work properly
        /// </summary>
        /// <param name="plugin">the plugin that is being requested by a client</param>
        /// <returns>a value indicating whether the provided plugin requires security</returns>
        public static bool RequiresSecurity(this object plugin)
        {
            return TypeRequiresSecurity(plugin?.GetType());
        }

        /// <summary>
        /// Checks on a type whether its instances require a secure channel - for objects that do not exist yet
        /// </summary>
        /// <param name="pluginType">the type of the plugin that is being requested by a client</param>
        /// <returns>a value indicating whether instances of the provided type require security</returns>
        /// <remarks>Deliberately not an extension on Type: <c>someType.RequiresSecurity()</c> would otherwise
        /// silently mean something else depending on whether the expression is typed as Type or as object.</remarks>
        public static bool TypeRequiresSecurity(Type pluginType)
        {
            if (pluginType == null)
            {
                return false;
            }

            return Attribute.IsDefined(pluginType, typeof(UseSecurityAttribute));
        }
    }
}
