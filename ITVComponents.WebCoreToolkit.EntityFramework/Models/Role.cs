using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
{
    [Index(nameof(RoleNameUniqueness),IsUnique=true,Name="IX_UniqueRoleName")]
    public class Role:ITVComponents.WebCoreToolkit.Models.Role
    {
        public Role()
        {
        }

        [Key]
        public int RoleId { get; set; }
        
        public int TenantId { get;set; }
        
        public bool IsSystemRole { get; set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string RoleNameUniqueness { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
