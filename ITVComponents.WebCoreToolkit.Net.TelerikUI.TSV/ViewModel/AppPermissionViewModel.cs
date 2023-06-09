using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class AppPermissionViewModel
    {
        public string PermissionSetName { get; set; }
        public int ParentId { get; set; }

        public int AppPermissionSetId { get; set; }

        public bool Assigned { get; set; }

        public string UniQUID { get; set; }
    }
}
