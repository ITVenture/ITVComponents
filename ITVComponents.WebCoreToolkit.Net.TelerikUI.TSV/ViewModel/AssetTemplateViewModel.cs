using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class AssetTemplateViewModel
    {
        public int AssetTemplateId { get; set; }

        public int? FeatureId { get; set; }

        public int? PermissionId { get; set; }

        public string Name { get; set; }

        public string SystemKey { get; set; }
    }
}
