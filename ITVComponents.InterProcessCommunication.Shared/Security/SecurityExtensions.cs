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
            Type t = plugin?.GetType();
            if (t == null)
            {
                return false;
            }

            return Attribute.IsDefined(t, typeof(UseSecurityAttribute));
        }
    }
}
