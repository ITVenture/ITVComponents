using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A currency-specific price of an add-on under a specific plan (see <see cref="PlanAddOn"/>). One row per
    /// supported currency; the provider holds one immutable Price per row, created against the add-on's product
    /// with the recurring interval of the owning plan. Replaces the former plan-agnostic AddOnPrice.
    /// </summary>
    [Index(nameof(PlanAddOnId), nameof(Currency), IsUnique = true, Name = "IX_UniquePlanAddOnPrice")]
    public class PlanAddOnPrice
    {
        [Key]
        public int PlanAddOnPriceId { get; set; }

        public int PlanAddOnId { get; set; }

        /// <summary>ISO-4217 currency code of <see cref="Amount"/>.</summary>
        [Required, MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        /// <summary>Recurring amount per the owning plan's <c>BillingInterval</c>, in <see cref="Currency"/>.</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Provider price identifier, set when the owning plan is pushed to the provider (immutable — an amount
        /// change creates a new price and re-points this field).
        /// </summary>
        [MaxLength(256)]
        public string? ProviderPriceId { get; set; }

        [ForeignKey(nameof(PlanAddOnId))]
        public virtual PlanAddOn? PlanAddOn { get; set; }
    }
}
