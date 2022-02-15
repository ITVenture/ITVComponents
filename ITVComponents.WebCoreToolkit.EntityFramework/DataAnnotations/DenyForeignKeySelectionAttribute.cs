using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations
{
    [AttributeUsage(validOn: AttributeTargets.Property | AttributeTargets.Class, Inherited = true)]
    public class DenyForeignKeySelectionAttribute:Attribute
    {
    }
}
