using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class LocalizationStringViewModel
    {
        public int LocalizationStringId { get; set; }

        [Required, MaxLength(256)]
        public string LocalizationKey { get; set; }

        [Required]
        public string LocalizationValue { get; set; }
    }
}
