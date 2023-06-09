using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class ClientAppTemplateViewModel
    {
        public int ClientAppTemplateId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }
    }
}
