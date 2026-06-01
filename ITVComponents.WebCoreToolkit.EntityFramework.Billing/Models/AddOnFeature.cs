using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A feature granted by an <see cref="AddOn"/>, referenced by string <see cref="FeatureKey"/> (see
    /// <see cref="PlanFeature"/> for the decoupling rationale).
    /// </summary>
    [Index(nameof(AddOnId), nameof(FeatureKey), IsUnique = true, Name = "IX_UniqueAddOnFeature")]
    public class AddOnFeature
    {
        [Key]
        public int AddOnFeatureId { get; set; }

        public int AddOnId { get; set; }

        [Required, MaxLength(256)]
        public string FeatureKey { get; set; } = string.Empty;

        [ForeignKey(nameof(AddOnId))]
        public virtual AddOn? AddOn { get; set; }
    }
}
