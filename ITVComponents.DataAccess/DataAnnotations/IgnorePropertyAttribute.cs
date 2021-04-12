using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DataAccess.DataAnnotations
{
    /// <summary>
    /// Mark Properties of Models with this Attribute if you don't want the ToViewModel Extension method to set this value
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IgnorePropertyAttribute:Attribute
    {
    }
}
