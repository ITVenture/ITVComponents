using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class VideoTutorial
    {
        [Key]
        public int VideoTutorialId { get; set; }

        [Required, MaxLength(200)]
        public string SortableName { get; set; }

        [Required]
        public string DisplayName { get; set; }

        public string Description { get; set; }

        public string ModuleUrl { get; set; }

        public virtual ICollection<TutorialStream> Streams { get; set; } = new List<TutorialStream>();
    }
}
