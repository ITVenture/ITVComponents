using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.Model
{
    public class User : IdentityUser
    {
        public int? AuthenticationTypeId { get; set; }

        [ForeignKey(nameof(AuthenticationTypeId))]
        public virtual AuthenticationType AuthenticationType { get; set; }

        public virtual ICollection<HierarchyTenantUser> TenantUsers { get; set; } = new List<HierarchyTenantUser>();

        public virtual ICollection<CustomUserProperty> UserProperties { get; set; } = new List<CustomUserProperty>();
    }
}
