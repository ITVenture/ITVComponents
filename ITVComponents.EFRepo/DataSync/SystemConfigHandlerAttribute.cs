using System;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Declares, on a <see cref="ConfigExtensionMarkup"/> subtype, its section key (the polymorphism
    /// discriminator) and the <see cref="IConfigExtension"/> handler that describes and compares it. Read by
    /// <c>AddSystemConfigExtension&lt;TMarkup&gt;()</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SystemConfigHandlerAttribute : Attribute
    {
        public SystemConfigHandlerAttribute(string sectionKey, Type handlerType)
        {
            SectionKey = sectionKey;
            HandlerType = handlerType;
        }

        public string SectionKey { get; }

        /// <summary>The handler type; must implement <see cref="IConfigExtension"/>.</summary>
        public Type HandlerType { get; }
    }
}
