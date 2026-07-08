using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// An à-la-carte add-on that can be subscribed alongside a base <see cref="Plan"/> — but only for a plan it
    /// is linked to via <see cref="PlanAddOn"/>. In the payment provider it is a single product (its identity);
    /// its price and recurring interval are per-plan and live on the link (<see cref="PlanAddOn.Prices"/>),
    /// since the interval is inherited from the owning plan. Grants one or more features via
    /// <see cref="AddOnFeature"/> (plan-independent — an add-on grants the same capability wherever booked).
    /// </summary>
    [Index(nameof(Name), IsUnique = true, Name = "IX_UniqueAddOn")]
    public class AddOn
    {
        [Key]
        public int AddOnId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Provider product identifier, set when pushed to the provider (identity, one per add-on).</summary>
        [MaxLength(256)]
        public string? ProviderProductId { get; set; }

        /// <summary>Plans this add-on is bookable for; the pricing lives on each link. See <see cref="PlanAddOn"/>.</summary>
        public virtual ICollection<PlanAddOn> PlanAddOns { get; set; } = new List<PlanAddOn>();

        public virtual ICollection<AddOnFeature> Features { get; set; } = new List<AddOnFeature>();
    }
}
