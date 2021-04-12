using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.Security.PermissionHandling
{
    /// <summary>
    /// PermissionHandler options are used to determine for which authentication-schemes the permissions can be evaluated
    /// </summary>
    public class PermissionPolicyOptions
    {
        /// <summary>
        /// Gets a list of supported Schemes. Add any Scheme you want to support to this collection
        /// </summary>
        public List<string> SupportedAuthenticationSchemes { get; } = new List<string>();
    }
}
