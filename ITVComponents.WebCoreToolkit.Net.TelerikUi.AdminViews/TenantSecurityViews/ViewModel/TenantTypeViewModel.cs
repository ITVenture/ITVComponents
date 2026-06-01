using ITVComponents.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class TenantTypeViewModel
    {
        public int TenantTypeId { get; set; }

        [MaxLength(512), Required]
        public string TenantTypeName { get; set; }

        [ExcludeFromDictionary]
        [DataType(DataType.MultilineText)]
        public string? TypeMetaData { get; set; }

        public int? TenantTemplateId { get; set; }
    }
}
