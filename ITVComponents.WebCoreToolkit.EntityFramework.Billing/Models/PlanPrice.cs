using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A currency-specific price of a <see cref="Plan"/>. A plan carries one price per supported currency; the
    /// provider holds one immutable Price object per row (a subscription is single-currency, so checkout picks
    /// the row matching the chosen currency). The recurring interval lives on the owning <see cref="Plan"/>
    /// (currency-agnostic), only amount + currency + provider id are per-row.
    /// </summary>
    [Index(nameof(PlanId), nameof(Currency), IsUnique = true, Name = "IX_UniquePlanPrice")]
    public class PlanPrice
    {
        [Key]
        public int PlanPriceId { get; set; }

        public int PlanId { get; set; }

        /// <summary>ISO-4217 currency code of <see cref="Amount"/> (e.g. "EUR", "CHF").</summary>
        [Required, MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        /// <summary>Recurring amount per the plan's <c>BillingInterval</c>, in <see cref="Currency"/>.</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Provider price identifier (e.g. Stripe price-ID), set when the plan is pushed to the provider.
        /// Provider prices are immutable — an amount change creates a new price and re-points this field.
        /// </summary>
        [MaxLength(256)]
        public string? ProviderPriceId { get; set; }

        [ForeignKey(nameof(PlanId))]
        public virtual Plan? Plan { get; set; }
    }
}
