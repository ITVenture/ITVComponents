using System;
using System.Collections.Generic;
using System.Text;
using ITVComponents.Plugins;

namespace ITVComponents.WebCoreToolkit.ServiceShared.FileHandling
{
    public interface IFileReasonPermissionProvider:IPlugin
    {
        /// <summary>
        /// Provides a list of Permissions that a user must have any of, to perform a specific task
        /// </summary>
        /// <param name="reason">the reason why this component is being invoked</param>
        /// <returns>a list of required permissions</returns>
        string[] PermissionsForReason(string reason);
    }
}
