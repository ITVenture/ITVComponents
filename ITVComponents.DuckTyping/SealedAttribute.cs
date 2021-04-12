using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.DuckTyping
{
    /// <summary>
    /// Marks a member as sealed. A StaticWrapper instance that wrapps another will only hide non-Sealed members
    /// </summary>
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Method|AttributeTargets.Property, AllowMultiple=false)]
    public class SealedAttribute:Attribute
    {
    }
}
