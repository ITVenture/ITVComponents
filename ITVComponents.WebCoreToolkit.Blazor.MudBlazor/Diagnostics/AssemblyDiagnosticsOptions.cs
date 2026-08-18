namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Diagnostics;

/// <summary>
/// Host configuration for the AssemblyDiagnostics view. Read through
/// <c>IHierarchySettings&lt;AssemblyDiagnosticsOptions&gt;</c>, so the value can be supplied from the
/// database via Global- or Tenant-Settings (requires <c>UseHierarchySettings()</c> + activated scoped/global
/// settings on the host) and falls back to a default instance when not configured. When
/// <see cref="UseConfigExchange"/> is false (default) the Configuration-Exchange tab is hidden.
/// </summary>
public class AssemblyDiagnosticsOptions
{
    public bool UseConfigExchange { get; set; }

    /// <summary>Name of the FileHandler plugin that receives an uploaded configuration (route {UploadModule}).</summary>
    public string ConfigUploadModule { get; set; } = "ConfigExchange";

    /// <summary>Upload reason passed to the handler (route {UploadReason}); drives the permission lookup.</summary>
    public string ConfigUploadReason { get; set; } = "ApplyConfig";

    public string ConfigDownloadReason { get; set; }

    /// <summary>Comma-separated file picker filter for the config upload, e.g. ".json,.zip".</summary>
    public string ConfigAccept { get; set; } = ".json";

    public string ConfigDownloadIdentifier { get; set; }

    /// <summary>
    /// File-type hint sent with an uploaded configuration. Optional: when empty the download identifier is used
    /// with any export-profile suffix stripped. The two are deliberately separate — the download carries the
    /// chosen profile (<c>sysCfg@Help</c>), while the upload must stay on the plain type, because what gets
    /// compared is decided by the content of the uploaded file and not by what was picked for the download.
    /// </summary>
    public string? ConfigUploadIdentifier { get; set; }

    /// <summary>
    /// Optional explicit <c>IConfigurationHandler</c> plugin name used when applying the reviewed configuration
    /// changes. When empty, the default registered handler is used (mirrors the MVC <c>ExplicitConfigHandler</c>).
    /// </summary>
    public string? ConfigHandlerName { get; set; }
}
