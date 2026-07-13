using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>
    /// The localized payload of a <see cref="HelpTopic"/>: a <see cref="Title"/> (shown in the tree, for
    /// containers and content pages alike) and, for content pages, a Markdown <see cref="Body"/>. The
    /// <see cref="Culture"/> is a BCP-47 tag (e.g. <c>de</c>, <c>de-CH</c>) or the literal <c>DEFAULT</c> used
    /// as the fallback when no more specific culture matches.
    /// </summary>
    [Index(nameof(HelpTopicId), nameof(Culture), IsUnique = true, Name = "IX_UniqueHelpTopicContent")]
    public class HelpTopicContent
    {
        [Key]
        public int HelpTopicContentId { get; set; }

        public int HelpTopicId { get; set; }

        /// <summary>BCP-47 culture (e.g. <c>de</c>, <c>de-CH</c>) or the literal <c>DEFAULT</c> fallback.</summary>
        [Required, MaxLength(35)]
        public string Culture { get; set; } = string.Empty;

        [Required, MaxLength(400)]
        public string Title { get; set; } = string.Empty;

        /// <summary>Markdown body. Null/empty for a container topic.</summary>
        public string? Body { get; set; }

        [ForeignKey(nameof(HelpTopicId))]
        public virtual HelpTopic? Topic { get; set; }
    }
}
