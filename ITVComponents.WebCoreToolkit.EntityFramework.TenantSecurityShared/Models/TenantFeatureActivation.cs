using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Models
{
    public class TenantFeatureActivation<TTenant>
    where TTenant : Tenant
    {
        [Key]
        public int TenantFeatureActivationId { get; set; }

        public int FeatureId { get; set; }

        public int TenantId { get; set; }

        public DateTime? ActivationStart { get; set; }

        public DateTime? ActivationEnd { get; set; }

        [ForeignKey(nameof(FeatureId))]
        public virtual Feature Feature { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual TTenant Tenant { get; set; }
    }
}
