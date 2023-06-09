using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class PermissionSetViewModel
    {
        public int AppPermissionSetId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; }
    }
}
