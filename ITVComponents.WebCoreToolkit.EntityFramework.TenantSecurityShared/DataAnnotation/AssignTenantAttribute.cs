using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.DataAnnotation
{
    [AttributeUsage(AttributeTargets.Property, Inherited=true)]
    public class AssignTenantAttribute:Attribute
    {
    }
}
