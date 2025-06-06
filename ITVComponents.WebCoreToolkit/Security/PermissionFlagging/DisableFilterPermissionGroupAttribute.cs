using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Property)]
    public class DisableFilterPermissionGroupAttribute:Attribute
    {
        public string PermissionGroupName { get; }

        public DisableFilterPermissionGroupAttribute(string permissionGroupName)
        {
            PermissionGroupName = permissionGroupName;
        }
    }
}
