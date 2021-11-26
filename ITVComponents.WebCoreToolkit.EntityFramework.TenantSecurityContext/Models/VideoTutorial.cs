using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
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
