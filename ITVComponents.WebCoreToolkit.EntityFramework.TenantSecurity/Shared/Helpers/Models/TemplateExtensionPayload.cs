using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// Polymorphic base for a decoupled tenant-template part payload (<see cref="TemplateExtensionMarkup.Payload"/>).
    /// A feature library defines a concrete subtype and registers it for native-polymorphism via
    /// <c>JsonHelper.ExtendNativeProtocolType&lt;TemplateExtensionPayload, TPayload&gt;(partKey)</c>, so the part is
    /// stored as typed, human-readable JSON in the template (no opaque payload string). Kept concrete so an unknown
    /// discriminator — a part whose contributing library is not installed — falls back to the base and is skipped
    /// on apply instead of failing the whole template deserialization.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    public class TemplateExtensionPayload
    {
    }
}
