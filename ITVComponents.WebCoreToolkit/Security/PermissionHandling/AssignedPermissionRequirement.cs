using Microsoft.AspNetCore.Authorization;

namespace ITVComponents.WebCoreToolkit.Security.PermissionHandling
{
    /// <summary>
    /// Represents the requirement of one ore more specific permissions for a user
    /// </summary>
    public class AssignedPermissionRequirement:IAuthorizationRequirement
    {
        /// <summary>
        /// A list of required permissions that are required for a specific action
        /// </summary>
        public string[] RequiredPermissions{ get; }


        /// <summary>
        /// Initializes a new instance of the AssignedPermissionRequirement class
        /// </summary>
        /// <param name="requiredPermissions">a lsit of required permissions</param>
        public AssignedPermissionRequirement(string[] requiredPermissions)
        {
            RequiredPermissions = requiredPermissions;
        }
    }
}
