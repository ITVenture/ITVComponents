using System;
using System.Collections.Generic;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>Registered system-config export extensions. Populated by <c>AddSystemConfigExtension&lt;TMarkup&gt;()</c>.</summary>
    public class ConfigExtensionOptions
    {
        public List<ConfigExtensionRegistration> Handlers { get; } = new();
    }

    /// <summary>One registered extension: its section key plus the markup and handler types.</summary>
    public record ConfigExtensionRegistration(string SectionKey, Type MarkupType, Type HandlerType);
}
