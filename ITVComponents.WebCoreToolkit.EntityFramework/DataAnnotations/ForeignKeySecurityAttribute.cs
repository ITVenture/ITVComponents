using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    [AttributeUsage(validOn: AttributeTargets.Property, Inherited =true)]
    public class ForeignKeySecurityAttribute:Attribute
    {
        public string[] RequiredPermissions { get; }

        public ForeignKeySecurityAttribute(params string[] requiredPermissions)
        {
            RequiredPermissions = requiredPermissions;
        }
    }
}
