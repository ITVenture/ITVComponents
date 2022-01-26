using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(NameUniqueness), IsUnique=true, Name="IX_UniquePluginConst")]
    public class WebPluginConstant
    {
        public int WebPluginConstantId { get; set; }

        [Required, MaxLength(128)]
        public string Name { get;set; }

        [Required]
        public string Value { get;set; }
        
        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string NameUniqueness { get; set; }
        
        public int? TenantId { get;set; }
        
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; }
    }
}
