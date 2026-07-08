using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// Bookable association of an <see cref="AddOn"/> to a <see cref="Plan"/> (n:m). An add-on can only be
    /// purchased alongside a plan for which such a link exists — the checkout validates against these rows.
    /// The pricing of the add-on lives on the link (see <see cref="PlanAddOnPrice"/>), not on the add-on
    /// itself: the same add-on may cost differently under different plans, and it inherits the owning plan's
    /// recurring interval (a Stripe subscription is single-interval), so its provider price is per-link.
    /// </summary>
    [Index(nameof(PlanId), nameof(AddOnId), IsUnique = true, Name = "IX_UniquePlanAddOn")]
    public class PlanAddOn
    {
        [Key]
        public int PlanAddOnId { get; set; }

        public int PlanId { get; set; }

        public int AddOnId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public virtual Plan? Plan { get; set; }

        [ForeignKey(nameof(AddOnId))]
        public virtual AddOn? AddOn { get; set; }

        /// <summary>
        /// Per-currency prices of this add-on <em>under this plan</em> (one row per supported currency). The
        /// recurring interval is the owning <see cref="Plan"/>'s <c>BillingInterval</c>, not stored here.
        /// </summary>
        public virtual ICollection<PlanAddOnPrice> Prices { get; set; } = new List<PlanAddOnPrice>();
    }
}
