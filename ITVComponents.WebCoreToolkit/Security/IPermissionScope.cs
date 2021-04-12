using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security
{
    /// <summary>
    /// Enables applications to add context-driven prefixes to user-permissions
    /// </summary>
    public interface IPermissionScope
    {
        /// <summary>
        /// Gets the permission prefix for the current user in this context
        /// </summary>
        string PermissionPrefix { get; }
        
        /// <summary>
        /// Gets a value indicating whether the PermissionScope was set explicitly
        /// </summary>
        bool IsScopeExplicit{get;}

        /// <summary>
        /// Sets the permissionScope to a new value
        /// </summary>
        /// <param name="newScope">the new scope to apply for the current user</param>
        void ChangeScope(string newScope);
    }
}
