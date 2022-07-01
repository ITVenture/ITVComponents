using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Helpers
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
    public class ExcludeFromDictionaryAttribute:Attribute
    {
    }
}
