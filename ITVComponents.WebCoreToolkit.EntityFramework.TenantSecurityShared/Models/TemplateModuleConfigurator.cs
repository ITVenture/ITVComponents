using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class TemplateModuleConfigurator
    {
        [Key]
        public int TemplateModuleConfiguratorId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(2048)]
        public string CustomConfiguratorView { get; set; }

        [Required, MaxLength(2048)]
        public string ConfiguratorTypeBack { get; set; }

        public int TemplateModuleId { get; set; }

        public string DisplayName { get; set; }

        [ForeignKey(nameof(TemplateModuleId))]
        public virtual TemplateModule ParentModule { get; set; }

        public virtual ICollection<TemplateModuleConfiguratorParameter> ViewComponentParameters { get; set; } =
            new List<TemplateModuleConfiguratorParameter>();
    }
}
