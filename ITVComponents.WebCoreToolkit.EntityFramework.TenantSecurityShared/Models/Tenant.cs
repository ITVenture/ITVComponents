using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(TenantName),IsUnique=true,Name="IX_UniqueTenant")]
    public class Tenant
    {
        [Key]
        public int TenantId { get; set; }

        [Required,MaxLength(150)]
        public string TenantName { get; set; }

        [MaxLength(1024)]
        public string DisplayName { get; set; }

        [MaxLength(125),ExcludeFromDictionary]
        public string TenantPassword { get; set; }

        [MaxLength(1024)]
        public string TimeZone { get; set; }

        public int? TenantTypeId { get; set; }

        public bool? TenantDirty { get; set; }

        [ForeignKey(nameof(TenantTypeId))]
        public virtual TenantType? TenantType { get; set; }
    }
}
