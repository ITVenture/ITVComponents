using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class RoleTemplateMarkup
    {
        public string Name { get; set; }

        public bool IsSystemRole { get; set; }

        public PermissionTemplateMarkup[] Permissions { get; set; }
    }
}
