using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models
{
    /// <summary>
    /// An à-la-carte add-on that can be subscribed alongside a base <see cref="Plan"/>. In the payment
    /// provider it is an additional subscription-item (its own price) on the same subscription. Grants one or
    /// more features via <see cref="AddOnFeature"/>.
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

        public BillingInterval BillingInterval { get; set; }

        /// <summary>Provider product identifier, set when pushed to the provider.</summary>
        [MaxLength(256)]
        public string? ProviderProductId { get; set; }

        /// <summary>
        /// Per-currency prices of this add-on (one row per supported currency). See <see cref="AddOnPrice"/>.
        /// </summary>
        public virtual ICollection<AddOnPrice> Prices { get; set; } = new List<AddOnPrice>();

        public virtual ICollection<AddOnFeature> Features { get; set; } = new List<AddOnFeature>();
    }
}
