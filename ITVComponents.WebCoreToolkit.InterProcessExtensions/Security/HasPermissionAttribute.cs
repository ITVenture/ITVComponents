using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.InterProcessExtensions.Security
{
    /// <summary>
    /// Provides a security extension for any class that may be exposed with a server
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Event | AttributeTargets.Property, AllowMultiple=true, Inherited=true)]
    public class HasPermissionAttribute : Attribute
    {
        /// <summary>
        /// Holds the provided permissions
        /// </summary>
        private readonly string[] requiredPermissions;

        /// <summary>
        /// Initializes a new instance of the HasPermissionsAttribute class
        /// </summary>
        /// <param name="requiredPermissions">the permissions required to access a specific class or member</param>
        public HasPermissionAttribute(params string[] requiredPermissions)
        {
            this.requiredPermissions = requiredPermissions;
        }

        /// <summary>
        /// When applied on a property, AllowWrite indicates whether the property is writable with the provided permissions
        /// </summary>
        public bool AllowWrite { get; set; } = false;

        /// <summary>
        /// When applied on a property, AllowRead indicates whether the property is readable with the provided permissions
        /// </summary>
        public bool AllowRead { get; set; } = false;

        /// <summary>
        /// Gets the required permissions that were provided to this attribute
        /// </summary>
        public string[] RequiredPermissions => requiredPermissions;
    }
}
