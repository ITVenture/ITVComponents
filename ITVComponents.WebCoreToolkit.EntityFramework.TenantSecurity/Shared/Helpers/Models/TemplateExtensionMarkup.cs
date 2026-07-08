namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// One decoupled template part in <see cref="TenantTemplateMarkup.Extensions"/>: the handler's typed
    /// <see cref="Payload"/> (a polymorphic <see cref="TemplateExtensionPayload"/> subtype) plus the
    /// <see cref="ApplyMode"/> that part should be applied with. Serialized as typed JSON via native polymorphism.
    /// </summary>
    public class TemplateExtensionMarkup
    {
        public TemplateExtensionMarkup()
        {
        }

        public TemplateExtensionMarkup(TemplateExtensionPayload payload)
        {
            Payload = payload;
        }

        /// <summary>The part handler's typed payload (polymorphic; discriminator = the handler's part key).</summary>
        public TemplateExtensionPayload Payload { get; set; }

        /// <summary>Apply mode for this part (<see cref="TemplateApplyMode.Auto"/> inherits the applying method's mode).</summary>
        public TemplateApplyMode ApplyMode { get; set; }
    }
}
