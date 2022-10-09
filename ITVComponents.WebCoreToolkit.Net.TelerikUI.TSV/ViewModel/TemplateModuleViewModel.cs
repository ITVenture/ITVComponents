using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TemplateModuleViewModel
    {
        public int TemplateModuleId { get; set; }

        [Required, MaxLength(255)]
        public string TemplateModuleName { get; set; }

        public int FeatureId { get; set; }
    }
}
