using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class TemplateModuleConfiguratorParameter
    {
        [Key]
        public int TemplateModuleCfgParameterId { get; set; }

        [Required, MaxLength(1024)]
        public string ParameterName { get; set; }

        [Required, MaxLength(4096)]
        public string ParameterValue { get; set; }

        public int TemplateModuleConfiguratorId { get; set; }

        [ForeignKey(nameof(TemplateModuleConfiguratorId))]
        public virtual TemplateModuleConfigurator ParentConfigurator { get; set; }
    }
}
