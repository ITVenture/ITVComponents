namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Polymorphic base for a system-configuration export extension section. Concrete subtypes are registered
    /// per feature library via <c>AddSystemConfigExtension&lt;TMarkup&gt;()</c>, which wires the System.Text.Json
    /// polymorphism entirely through <c>JsonHelper.ExtendNativeProtocolType</c> (no static <c>[JsonPolymorphic]</c>
    /// attribute) so the section round-trips as typed JSON under <c>SerializationTypingMode.NativePolymorphism</c>.
    /// Because the polymorphism is built at registration time, a system on which <b>no</b> config extension is
    /// installed keeps this base non-polymorphic and serializes it plainly, instead of failing the whole export.
    /// Kept concrete (not abstract) so an unknown discriminator — a section whose contributing library is not
    /// installed on the importing system — falls back to the base type and is simply skipped instead of failing
    /// the whole import.
    /// </summary>
    public class ConfigExtensionMarkup
    {
        /// <summary>Section key; equals the registered polymorphism discriminator and <see cref="IConfigExtension.SectionKey"/>.</summary>
        public string SectionKey { get; set; } = string.Empty;
    }
}
