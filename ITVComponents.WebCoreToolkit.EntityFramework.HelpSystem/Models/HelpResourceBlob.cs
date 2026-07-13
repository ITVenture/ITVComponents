using System;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>
    /// Backing store row for the built-in reference <c>IHelpResourceStore</c> (EF blob). Holds the raw bytes of
    /// an uploaded help resource keyed by the opaque <see cref="FileIdentifier"/> that
    /// <see cref="HelpResourceFile.FileIdentifier"/> points at. Hosts with their own storage (Azure, DMS, …)
    /// implement <c>IHelpResourceStore</c> against their backend and never touch this table.
    /// </summary>
    public class HelpResourceBlob
    {
        /// <summary>Opaque identifier assigned on save; referenced by <see cref="HelpResourceFile.FileIdentifier"/>.</summary>
        [Key, MaxLength(512)]
        public string FileIdentifier { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? ContentType { get; set; }

        [MaxLength(400)]
        public string? DownloadName { get; set; }

        public byte[] Content { get; set; } = Array.Empty<byte>();

        public DateTime Created { get; set; }
    }
}
