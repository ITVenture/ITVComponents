namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Configuration
{
    /// <summary>
    /// WebPart activation options for the help system-config export section. When
    /// <see cref="ActivateHelpConfigExport"/> is set, the WebPart contributes the help tree and resource library
    /// as a <c>ConfigExtensionMarkup</c> subtype (via <c>AddHelpConfigExtension</c>). Requires a config-handler
    /// host (TenantSecurity) whose context also implements <c>IHelpSystemContext</c>.
    /// </summary>
    public class HelpConfigExportPartOptions
    {
        /// <summary>
        /// When <c>true</c>, the help system is registered as a system-config export section. Off by default so a
        /// host that does not want its documentation in the system config simply omits it (the base section stays
        /// non-polymorphic and serializes without help).
        /// </summary>
        public bool ActivateHelpConfigExport { get; set; }

        /// <summary>
        /// How much of the resource library travels with the section. Optional — omitted means the defaults of
        /// <see cref="HelpConfigExportOptions"/> (contents included, 2 MB per file, 20 MB overall).
        /// </summary>
        public HelpConfigExportOptions? Contents { get; set; }
    }
}
