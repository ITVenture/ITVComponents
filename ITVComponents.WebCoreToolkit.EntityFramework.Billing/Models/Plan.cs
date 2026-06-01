using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// Globally-defined base billing plan a tenant can subscribe to. A plan bundles a fixed set of
    /// <see cref="PlanFeature"/>s; additional capabilities can be purchased à-la-carte as <see cref="AddOn"/>s.
    /// Provider-agnostic — the concrete payment-provider identifiers live in
    /// <see cref="ProviderProductId"/> / <see cref="ProviderPriceId"/> and are populated when the plan is
    /// pushed to the provider (toolkit-is-source-of-truth, see [[itvcomponents-billing-spec]]).
    /// </summary>
    [Index(nameof(Name), IsUnique = true, Name = "IX_UniquePlan")]
    public class Plan
    {
        [Key]
        public int PlanId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1024)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public BillingInterval BillingInterval { get; set; }

        /// <summary>ISO-4217 currency code of <see cref="Amount"/> (e.g. "EUR", "CHF").</summary>
        [MaxLength(3)]
        public string? Currency { get; set; }

        /// <summary>Recurring amount per <see cref="BillingInterval"/>, in the plan's <see cref="Currency"/>.</summary>
        public decimal Amount { get; set; }

        /// <summary>Optional trial-period in days. Null = no trial.</summary>
        public int? TrialDays { get; set; }

        /// <summary>Optional seat count if the plan meters seats.</summary>
        public int? SeatCount { get; set; }

        /// <summary>Provider product identifier (e.g. Stripe product-ID), set when pushed to the provider.</summary>
        [MaxLength(256)]
        public string? ProviderProductId { get; set; }

        /// <summary>
        /// Provider price identifier (e.g. Stripe price-ID), set when pushed to the provider. Provider prices
        /// are typically immutable — a price change creates a new price and re-points this field.
        /// </summary>
        [MaxLength(256)]
        public string? ProviderPriceId { get; set; }

        public virtual ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
    }
}
