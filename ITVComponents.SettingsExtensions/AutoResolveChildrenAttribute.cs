using System;

namespace ITVComponents.SettingsExtensions
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class AutoResolveChildrenAttribute:Attribute
    {
    }
}
