using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(GlobalRoleTemplateMarkup), "base")]
    public class GlobalRoleTemplateMarkup
    {
        public string RoleName { get; set; }

        public string? RoleDescription { get; set; }

        public string[] Permissions { get; set; }
    }
}
