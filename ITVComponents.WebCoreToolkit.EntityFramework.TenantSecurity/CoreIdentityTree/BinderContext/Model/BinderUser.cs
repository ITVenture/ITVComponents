using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ITVComponents.EFRepo.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentityTree.BinderContext.Model
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
