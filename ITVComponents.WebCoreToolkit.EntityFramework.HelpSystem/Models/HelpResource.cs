using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.HelpSystem.Models
{
    /// <summary>
    /// A named media resource (image / video / other) referenced from help content via its stable
    /// <see cref="Name"/> (e.g. <c>resource:{Name}</c> in Markdown). The actual bytes are NOT stored here —
    /// they live wherever the configured file-handler plugin puts them; each <see cref="HelpResourceFile"/>
    /// only carries the handler's file identifier. A resource can hold one file per culture.
    /// </summary>
    [Index(nameof(Name), IsUnique = true, Name = "IX_UniqueHelpResource")]
    public class HelpResource
    {
        [Key]
        public int HelpResourceId { get; set; }

        /// <summary>Unique, stable reference name used from Markdown (<c>resource:{Name}</c>).</summary>
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? Description { get; set; }

        public HelpResourceKind Kind { get; set; }

        /// <summary>The stored files, one per culture (<c>DEFAULT</c> is the fallback).</summary>
        public virtual ICollection<HelpResourceFile> Files { get; set; } = new List<HelpResourceFile>();
    }
}
