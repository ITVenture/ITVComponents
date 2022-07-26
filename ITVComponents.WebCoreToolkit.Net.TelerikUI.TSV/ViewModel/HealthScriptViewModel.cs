using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class HealthScriptViewModel
    {
        [Key]
        public int HealthScriptId { get; set; }

        [MaxLength(128)]
        public string HealthScriptName { get; set; }

        [DataType(DataType.MultilineText)]
        public string Script { get; set; }
    }
}
