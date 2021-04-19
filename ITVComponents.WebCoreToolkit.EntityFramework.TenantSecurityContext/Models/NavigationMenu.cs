using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ITVComponents.WebCoreToolkit.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Models
{
    [Index(nameof(UrlUniqueness), IsUnique = true, Name="IX_UniqueUrl")]
    public class NavigationMenu
    {
        public NavigationMenu()
        {
        }

        [Key]
        public int NavigationMenuId { get; set; }

        [Required]
        public string DisplayName { get; set; }

        [MaxLength(1024)]
        public string Url { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed),MaxLength(1024),Required]
        public string UrlUniqueness { get; set; }

        public int? ParentId { get; set; }

        public int? SortOrder { get; set; }

        public int? PermissionId { get; set; }

        public string SpanClass { get; set; }

        [ForeignKey(nameof(ParentId))]
        public virtual NavigationMenu Parent { get; set; }

        public virtual ICollection<NavigationMenu> Children { get;set; } = new List<NavigationMenu>();
        
        public virtual ICollection<TenantNavigationMenu> Tenants { get; set; } = new List<TenantNavigationMenu>();

        [ForeignKey(nameof(PermissionId))]
        public virtual Permission EntryPoint { get; set; }
    }
}
