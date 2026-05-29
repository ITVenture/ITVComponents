using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models
{
    public class TutorialStreamBlob
    {
        public int TutorialStreamBlobId { get; set; }

        [Required]
        public byte[] Content { get; set; }

        public int TutorialStreamId { get; set; }

        [ForeignKey(nameof(TutorialStreamId))]
        public virtual TutorialStream Parent { get; set; }
    }
}
