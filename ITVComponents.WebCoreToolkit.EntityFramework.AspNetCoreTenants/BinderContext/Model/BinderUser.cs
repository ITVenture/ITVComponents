using ITVComponents.EFRepo.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.BinderContext.Model
{
    [BinderEntity]
    public class BinderUser
    {
        [Key]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the user name for this user.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the email address for this user.
        /// </summary>
        public string Email { get; set; }

        public virtual ICollection<BinderTenantUser> TenantUsers { get; set; } = new List<BinderTenantUser>();
    }
}
