using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.PermissionFlagging
{
    public static class PermissionGroupExtensions
    {
        public static string[] GetPermissionGroupName(this Type type)
        {
            string[] retVal = ["Default"];
            if (Attribute.IsDefined(type, typeof(DisableFilterPermissionGroupAttribute), true))
            {
                var att = (DisableFilterPermissionGroupAttribute)Attribute.GetCustomAttribute(type,
                    typeof(DisableFilterPermissionGroupAttribute), true);
                retVal = att.PermissionGroupName;
            }

            return retVal;
        }

        public static string[] GetPermissionGroupName(this PropertyInfo prop, Type genericBaseType = null)
        {
            if (Attribute.IsDefined(prop, typeof(DisableFilterPermissionGroupAttribute), true))
            {
                var att = (DisableFilterPermissionGroupAttribute)Attribute.GetCustomAttribute(prop,
                    typeof(DisableFilterPermissionGroupAttribute), true);
                return att.PermissionGroupName;
            }

            if (genericBaseType != null && prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == genericBaseType)
            {
                var targetType = prop.PropertyType.GetGenericArguments().First();
                return targetType.GetPermissionGroupName();
            }

            return prop.PropertyType.GetPermissionGroupName();
        }
    }
}
