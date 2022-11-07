using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TemplateModuleConfiguratorParameterViewModel
    {
        public int TemplateModuleCfgParameterId { get; set; }

        [Required, MaxLength(1024)]
        public string ParameterName { get; set; }

        [Required,DataType(DataType.MultilineText)]
        public string ParameterValue { get; set; }

        public int TemplateModuleConfiguratorId { get; set; }
    }
}
