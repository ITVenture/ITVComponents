using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TemplateModuleConfiguratorParameterViewModel
    {
        public int TemplateModuleCfgParameterId { get; set; }

        [Required, MaxLength(1024)]
        public string ParameterName { get; set; }

        [Required, MaxLength(4096),DataType(DataType.MultilineText)]
        public string ParameterValue { get; set; }

        public int TemplateModuleConfiguratorId { get; set; }
    }
}
