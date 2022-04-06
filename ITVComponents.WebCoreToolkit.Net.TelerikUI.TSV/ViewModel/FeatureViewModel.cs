using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class FeatureViewModel
    {
        public int FeatureId { get; set; }

        [MaxLength(512), Required]
        public string FeatureName { get; set; }

        public string FeatureDescription { get; set; }

        public bool Enabled { get; set; }
        public int? TenantId { get; set; }
    }
}
