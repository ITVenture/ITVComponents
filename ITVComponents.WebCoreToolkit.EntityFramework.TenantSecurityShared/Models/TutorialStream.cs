using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
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
