using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class NavigationTemplateMarkup
    {
        public string Name { get; set; }

        public string UniqueKey { get; set; }

        public PermissionTemplateMarkup CustomPermission { get; set; }
    }
}
