using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Models
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
    }
}
