using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(RoleTemplateMarkup), "base")]
    public class RoleTemplateMarkup
    {
        public string Name { get; set; }

        public bool IsSystemRole { get; set; }

        public string[] Permissions { get; set; }

        public string[] RoleGrants { get; set; }

        public string[] GlobalRoleGrants { get; set; }
    }
}
