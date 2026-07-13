using ITVComponents.WebCoreToolkit.Configuration;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Options
{
    /// <summary>
    /// Global help-system configuration. Bind from configuration section <c>HelpSystem</c>. Resource storage is
    /// a plain DI service (<c>IHelpResourceStore</c>) — deliberately NOT the plugin file-handler path, so the
    /// public viewer can serve resources anonymously (the plugin factory is security-gated and cannot load a
    /// handler for an anonymous request).
    /// </summary>
    [SettingName("HelpSystem")]
    public class HelpSystemOptions
    {
        /// <summary>Master switch for the help module.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// When true, the public viewer serves resources anonymously through the resolver endpoint. When false,
        /// resource requests require an authenticated user.
        /// </summary>
        public bool AnonymousResourceAccess { get; set; } = true;

        /// <summary>Hard cap for a single resource upload in bytes (default 25 MB).</summary>
        public long MaxUploadBytes { get; set; } = 25L * 1024 * 1024;

        /// <summary>
        /// Optional allow-list of content-type prefixes accepted on upload (e.g. <c>image/</c>, <c>video/</c>).
        /// Null or empty accepts any type.
        /// </summary>
        public string[]? AllowedContentTypePrefixes { get; set; }
    }
}
