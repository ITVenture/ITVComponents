using System.Text.Json.Serialization;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Polymorphic base for a system-configuration export extension section. Concrete subtypes are registered
    /// per feature library via <c>AddSystemConfigExtension&lt;TMarkup&gt;()</c> (which wires System.Text.Json
    /// polymorphism through <c>JsonHelper.ExtendNativeProtocolType</c>, so the section round-trips as typed JSON
    /// under <c>SerializationTypingMode.NativePolymorphism</c>). Kept concrete (not abstract) so an unknown
    /// discriminator — a section whose contributing library is not installed on the importing system — falls back
    /// to the base type and is simply skipped instead of failing the whole import.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    public class ConfigExtensionMarkup
    {
        /// <summary>Section key; equals the registered polymorphism discriminator and <see cref="IConfigExtension.SectionKey"/>.</summary>
        public string SectionKey { get; set; } = string.Empty;
    }
}
