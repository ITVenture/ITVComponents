using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class TemplateModuleScript
    {
        [Key]
        public int TemplateModuleScriptId { get; set; }

        [Required, MaxLength(1024)]
        public string ScriptFile { get; set; }

        public int TemplateModuleId { get; set; }

        [ForeignKey(nameof(TemplateModuleId))]
        public virtual TemplateModule ParentModule { get; set; }
    }
}
