using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    /// <summary>
    /// Controls how one kind of tenant-template content (roles, plugins, settings, …) is applied to a tenant.
    /// Resolved per kind as <c>perKindMode == Auto ? methodDefault : perKindMode</c>, where a <c>methodDefault</c>
    /// of <see cref="Auto"/> itself resolves to <see cref="Forced"/>. Because the default value is <see cref="Auto"/>
    /// (0), a template that does not set a mode simply inherits the applying method's mode instead of silently
    /// winning as an explicit value.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TemplateApplyMode
    {
        /// <summary>
        /// Inherit: on a template kind, use the applying method's resolved mode; when passed to the applying method
        /// itself, it is treated as <see cref="Forced"/> (the engine's historical behaviour).
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Upsert only: create missing entries and update matching ones (by name/key). Tenant entries of this kind
        /// that are absent from the template are left untouched (never deleted).
        /// </summary>
        Additive = 1,

        /// <summary>
        /// Sync: like <see cref="Additive"/>, but additionally deletes tenant entries of this kind that are not part
        /// of the template, so the tenant ends up matching the template exactly for that kind.
        /// </summary>
        Forced = 2
    }
}
