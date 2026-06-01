using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A feature granted by a <see cref="Plan"/>. The feature is referenced by a string
    /// <see cref="FeatureKey"/> (not a foreign key) so that Billing stays decoupled from the tenant-security
    /// feature catalog: the <c>IFeatureProvisioner</c> adapter maps the key onto a concrete feature activation.
    /// </summary>
    [Index(nameof(PlanId), nameof(FeatureKey), IsUnique = true, Name = "IX_UniquePlanFeature")]
    public class PlanFeature
    {
        [Key]
        public int PlanFeatureId { get; set; }

        public int PlanId { get; set; }

        [Required, MaxLength(256)]
        public string FeatureKey { get; set; } = string.Empty;

        [ForeignKey(nameof(PlanId))]
        public virtual Plan? Plan { get; set; }
    }
}
