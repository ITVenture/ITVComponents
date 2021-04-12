using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.Plugins.SingletonPattern
{
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Interface)]
    public class SingletonAttribute:Attribute
    {
    }
}
