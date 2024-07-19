using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.EFRepo.DataAnnotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple=false,Inherited=true)]
    public class BinderEntityAttribute:Attribute
    {
    }
}
