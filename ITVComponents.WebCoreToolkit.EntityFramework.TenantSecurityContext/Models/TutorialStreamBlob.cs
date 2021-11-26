using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
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
