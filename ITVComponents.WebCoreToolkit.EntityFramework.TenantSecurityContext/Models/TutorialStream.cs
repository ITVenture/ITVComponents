using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    public class TutorialStream
    {
        [Key]
        public int TutorialStreamId { get; set; }

        [MaxLength(100), Required]
        public string LanguageTag { get; set; }

        [MaxLength(100), Required]
        public string ContentType { get; set; }

        public int VideoTutorialId { get; set; }

        [ForeignKey(nameof(VideoTutorialId))]
        public virtual VideoTutorial Parent { get; set; }

        public virtual TutorialStreamBlob Blob { get; set; }
    }
}
