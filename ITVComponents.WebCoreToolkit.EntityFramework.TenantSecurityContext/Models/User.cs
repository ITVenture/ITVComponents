using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    [Index(nameof(UserName),IsUnique=true,Name="IX_UniqueUserName")]
    public class User
    {
        public User()
        {
        }

        [Key]
        public int UserId { get; set; }

        [MaxLength(150)]
        [Required]
        public string UserName { get; set; }

        public int AuthenticationTypeId { get; set; }

        [ForeignKey(nameof(AuthenticationTypeId))]
        public virtual AuthenticationType AuthenticationType { get;set; }

        public virtual ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();

        public virtual ICollection<CustomUserProperty> UserProperties { get; set; } = new List<CustomUserProperty>();
    }
}
