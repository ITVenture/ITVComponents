using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    [Index(nameof(FeatureName), IsUnique = true, Name = "IX_FeatureUniqueness")]
    public class Feature
    {
        [Key]
        public int FeatureId { get; set; }

        [MaxLength(512), Required]
        public string FeatureName { get; set; }

        public string FeatureDescription { get; set; }

        public bool Enabled { get; set; }
    }
}
