using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>
    /// A culture-specific file binding of a <see cref="HelpResource"/>. Stores only the
    /// <see cref="FileIdentifier"/> the configured file handler reads the bytes back with (plus metadata) —
    /// the bytes themselves are owned by the handler plugin. <see cref="Culture"/> is a BCP-47 tag or the
    /// literal <c>DEFAULT</c> fallback.
    /// </summary>
    [Index(nameof(HelpResourceId), nameof(Culture), IsUnique = true, Name = "IX_UniqueHelpResourceFile")]
    public class HelpResourceFile
    {
        [Key]
        public int HelpResourceFileId { get; set; }

        public int HelpResourceId { get; set; }

        /// <summary>BCP-47 culture (e.g. <c>de</c>, <c>de-CH</c>) or the literal <c>DEFAULT</c> fallback.</summary>
        [Required, MaxLength(35)]
        public string Culture { get; set; } = string.Empty;

        /// <summary>The identifier the configured file handler uses to read the stored bytes back.</summary>
        [Required, MaxLength(512)]
        public string FileIdentifier { get; set; } = string.Empty;

        [MaxLength(256)]
        public string? ContentType { get; set; }

        [MaxLength(400)]
        public string? OriginalName { get; set; }

        [ForeignKey(nameof(HelpResourceId))]
        public virtual HelpResource? Resource { get; set; }
    }
}
