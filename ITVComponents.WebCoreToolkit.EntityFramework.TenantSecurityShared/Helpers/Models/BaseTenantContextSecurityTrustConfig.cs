using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class BaseTenantContextSecurityTrustConfig
    {
        public bool HideGlobals { get; set; }

        public bool ShowAllTenants { get; set; }
    }
}
