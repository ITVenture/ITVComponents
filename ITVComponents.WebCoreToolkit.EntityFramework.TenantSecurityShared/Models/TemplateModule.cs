using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class TemplateModule
    {
        [Key]
        public int TemplateModuleId { get; set; }

        [Required, MaxLength(255)]
        public string TemplateModuleName { get; set; }

        public int FeatureId { get; set; }

        [ForeignKey(nameof(FeatureId))]
        public virtual Feature RequiredFeature { get; set; }

        public virtual ICollection<TemplateModuleConfigurator> Configurators { get; set; } =
            new List<TemplateModuleConfigurator>();

        public virtual ICollection<TemplateModuleScript> Scripts { get; set; } = new List<TemplateModuleScript>();
    }
}
