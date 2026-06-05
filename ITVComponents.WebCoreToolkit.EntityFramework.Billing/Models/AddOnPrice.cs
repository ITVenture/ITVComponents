using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// A currency-specific price of an <see cref="AddOn"/> (see <see cref="PlanPrice"/> for the rationale). One
    /// row per supported currency; the provider holds one immutable Price per row.
    /// </summary>
    [Index(nameof(AddOnId), nameof(Currency), IsUnique = true, Name = "IX_UniqueAddOnPrice")]
    public class AddOnPrice
    {
        [Key]
        public int AddOnPriceId { get; set; }

        public int AddOnId { get; set; }

        /// <summary>ISO-4217 currency code of <see cref="Amount"/>.</summary>
        [Required, MaxLength(3)]
        public string Currency { get; set; } = string.Empty;

        /// <summary>Recurring amount per the add-on's <c>BillingInterval</c>, in <see cref="Currency"/>.</summary>
        public decimal Amount { get; set; }

        /// <summary>Provider price identifier, set when the add-on is pushed to the provider (immutable).</summary>
        [MaxLength(256)]
        public string? ProviderPriceId { get; set; }

        [ForeignKey(nameof(AddOnId))]
        public virtual AddOn? AddOn { get; set; }
    }
}
