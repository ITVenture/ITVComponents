using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.DataAccess.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class FeatureActivationViewModel
    {
        public int TenantFeatureActivationId { get; set; }

        public int FeatureId { get; set; }

        public int TenantId { get; set; }

        [UtcDateTime]
        public DateTime? ActivationStart { get; set; }
        
        [UtcDateTime]
        public DateTime? ActivationEnd { get; set; }
    }
}
