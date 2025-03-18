using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TrustedFullAccessComponentViewModel
    {
        public int TrustedFullAccessComponentId { get; set; }

        [Required, MaxLength(1024)]
        public string FullQualifiedTypeName { get; set; }

        [MaxLength(1024)]
        public string TargetQualifiedTypeName { get; set; }

        [DataType(DataType.MultilineText)]
        public string Description { get; set; }

        [DataType(DataType.MultilineText)]
        public string TrustLevelConfig { get; set; }
    }
}
