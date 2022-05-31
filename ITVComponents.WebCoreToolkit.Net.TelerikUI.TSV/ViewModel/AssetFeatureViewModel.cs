using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class AssetFeatureViewModel
    {
        [Key]
        public int FeatureId { get; set; }

        [MaxLength(150)]
        [Required]
        public string FeatureName { get; set; }

        [MaxLength(2048)]
        public string Description { get; set; }

        public bool Assigned { get; set; }
        public int AssetTemplateId{ get; set; }

        public string UniQUID { get; set; }
    }
}
