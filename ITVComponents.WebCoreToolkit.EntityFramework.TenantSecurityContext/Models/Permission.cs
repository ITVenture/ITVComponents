using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    [Index(nameof(PermissionNameUniqueness),IsUnique=true,Name="IX_UniquePermissionName")]
    [ForeignKeySelection("new ForeignKeyData<int> {Key = t.PermissionId, Label = t.PermissionName, FullRecord=t.ToDictionary(true)}", "orderby t.PermissionName")]
    public class Permission:ITVComponents.WebCoreToolkit.Models.Permission
    {
        public Permission()
        {
        }

        [Key]
        public int PermissionId { get; set; }

        [MaxLength(2048)]
        public string Description { get; set; }
        
        public int? TenantId { get; set; }

        //public bool IsGlobal { get;set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string PermissionNameUniqueness { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
