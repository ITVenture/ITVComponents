using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class LocalizationViewModel
    {
        public int LocalizationId { get; set; }

        [MaxLength(1024)]
        public string Identifier { get; set; }
    }
}
