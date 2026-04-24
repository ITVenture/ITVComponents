using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Property, AllowMultiple=true)]
    public class DisableFilterPermissionGroupAttribute:Attribute
    {
        private string permissionCategory = "General";
        public string[] PermissionGroupName { get; }

        public string PermissionCategory
        {
            get => permissionCategory??"General";
            set => permissionCategory = value;
        }

        public DisableFilterPermissionGroupAttribute(params string[] permissionGroupName)
        {
            PermissionGroupName = permissionGroupName;
        }
    }
}
