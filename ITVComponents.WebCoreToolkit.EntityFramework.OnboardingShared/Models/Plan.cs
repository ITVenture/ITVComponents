using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models
{
    /// <summary>
    /// Globally-defined billing plan a tenant can subscribe to. Provider-agnostic — concrete provider IDs
    /// (Stripe price IDs etc.) live in <see cref="ProviderProductId"/>.
    /// </summary>
    [Index(nameof(Name), IsUnique = true, Name = "IX_UniquePlan")]
    public class Plan
    {
        [Key]
        public int PlanId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }

        [MaxLength(1024)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Provider-specific product or price identifier (e.g. Stripe price-ID).
        /// </summary>
        [MaxLength(256)]
        public string ProviderProductId { get; set; }

        public BillingInterval BillingInterval { get; set; }

        /// <summary>
        /// Optional seat count if the plan meters seats.
        /// </summary>
        public int? SeatCount { get; set; }

        /// <summary>
        /// Optional trial-period in days. If null the plan has no trial.
        /// </summary>
        public int? TrialDays { get; set; }
    }
}
