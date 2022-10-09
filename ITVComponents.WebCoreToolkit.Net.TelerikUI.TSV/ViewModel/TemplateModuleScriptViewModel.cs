using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class TemplateModuleScriptViewModel
    {
        public int TemplateModuleScriptId { get; set; }

        [Required, MaxLength(1024)]
        public string ScriptFile { get; set; }

        public int TemplateModuleId { get; set; }
    }
}
