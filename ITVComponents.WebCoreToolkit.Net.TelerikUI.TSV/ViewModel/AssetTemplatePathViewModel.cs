using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class AssetTemplatePathViewModel
    {
        public int AssetTemplatePathId { get; set; }

        public int AssetTemplateId { get; set; }

        [MaxLength(1024)]
        public string PathTemplate { get; set; }
    }
}
