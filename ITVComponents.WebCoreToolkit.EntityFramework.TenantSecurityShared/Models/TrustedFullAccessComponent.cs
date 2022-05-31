using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(FullQualifiedTypeName), IsUnique=true, Name="UQ_TrustedComponentType")]
    public class TrustedFullAccessComponent
    {
        [Key]
        public int TrustedFullAccessComponentId { get; set; }

        [Required, MaxLength(1024)]
        public string FullQualifiedTypeName { get; set; }

        public string Description { get; set; }

        public bool TrustedForGlobals { get; set; }

        public bool TrustedForAllTenants { get; set; }
    }
}
