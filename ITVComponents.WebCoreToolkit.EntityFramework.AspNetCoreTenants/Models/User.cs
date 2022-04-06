using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models
{
    public class User: IdentityUser
    {
        public int? AuthenticationTypeId { get; set; }

        [ForeignKey(nameof(AuthenticationTypeId))]
        public virtual AuthenticationType AuthenticationType { get; set; }

        public virtual ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();

        public virtual ICollection<CustomUserProperty> UserProperties { get; set; } = new List<CustomUserProperty>();
    }
}
