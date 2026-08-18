namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Configuration
{
    /// <summary>
    /// Controls how much of the resource library travels with the help section of the system-config export.
    /// The bytes are carried inline as base64, which roughly inflates them by a third and — because the diff
    /// result travels back through the browser — moves each changed file twice. The limits below keep a single
    /// oversized asset from turning the configuration into an unusable download; what they cut is reported in
    /// the diff rather than dropped silently.
    /// </summary>
    public class HelpConfigExportOptions
    {
        /// <summary>
        /// Whether the stored bytes of a resource are exported alongside its metadata. On by default: a host that
        /// switches the help section on wants its documentation to arrive complete, and images are part of it.
        /// With this off, only metadata travels and the diff reports the file bindings it could not create.
        /// </summary>
        public bool IncludeResourceContents { get; set; } = true;

        /// <summary>
        /// Largest single file carried inline, in bytes (default 2 MB). Anything above is exported as metadata
        /// plus its hash, and the diff says so.
        /// </summary>
        public long MaxFileBytes { get; set; } = 2L * 1024 * 1024;

        /// <summary>
        /// Upper bound over all carried contents, in bytes (default 20 MB). Once reached, the remaining files
        /// travel as metadata only.
        /// </summary>
        public long MaxTotalBytes { get; set; } = 20L * 1024 * 1024;
    }
}
