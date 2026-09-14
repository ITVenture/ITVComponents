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

        /// <summary>
        /// The roles this role passes its OWN permissions ON TO - not the roles it draws permissions from. Each entry
        /// becomes a RoleRole with this role as PermissiveRole and the named role as PermittedRole, and the named role
        /// ends up with everything this one has. Reading it the other way round grants the weaker role the rights of
        /// the stronger one, and it does so without any error - the result is simply too much permission.
        /// </summary>
        public string[] RoleGrants { get; set; }

        public string[] GlobalRoleGrants { get; set; }
    }
}
