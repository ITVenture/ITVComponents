using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>
    /// A node in the global help tree. A topic is either a <see cref="HelpTopicKind.Container"/> (a grouping
    /// node without body) or a <see cref="HelpTopicKind.ContentPage"/> (carries localized Markdown in
    /// <see cref="Contents"/>). The tree is a self-referencing hierarchy via <see cref="ParentId"/>. Topics are
    /// global (system-wide product documentation) — there is intentionally no tenant reference.
    /// </summary>
    [Index(nameof(Slug), IsUnique = true, Name = "IX_UniqueHelpTopicSlug")]
    public class HelpTopic
    {
        [Key]
        public int HelpTopicId { get; set; }

        /// <summary>Parent node; null for a root topic.</summary>
        public int? ParentId { get; set; }

        public HelpTopicKind Kind { get; set; }

        /// <summary>Stable, URL-friendly identifier for deep-linking (<c>/help/{slug}</c>) and context help.</summary>
        [Required, MaxLength(200)]
        public string Slug { get; set; } = string.Empty;

        /// <summary>Ordering among siblings.</summary>
        public int SortOrder { get; set; }

        /// <summary>Optional MudBlazor icon name shown in the tree.</summary>
        [MaxLength(200)]
        public string? Icon { get; set; }

        /// <summary>Only published topics are shown in the public (anonymous) viewer; drafts stay admin-only.</summary>
        public bool IsPublished { get; set; }

        [ForeignKey(nameof(ParentId))]
        public virtual HelpTopic? Parent { get; set; }

        public virtual ICollection<HelpTopic> Children { get; set; } = new List<HelpTopic>();

        /// <summary>Localized title (+ Markdown body for content pages), one row per culture.</summary>
        public virtual ICollection<HelpTopicContent> Contents { get; set; } = new List<HelpTopicContent>();
    }
}
