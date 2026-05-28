using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Onboarding.Shared.Models
{
    /// <summary>
    /// Abstract base for billing-profile addresses (default / invoice).
    /// Concrete subclasses bind <typeparamref name="TBillingProfile"/> to the consumer-specific BillingProfile derivative.
    /// </summary>
    public abstract class AddressBase<TBillingProfile>
        where TBillingProfile : class
    {
        [Key]
        public int AddressId { get; set; }

        [MaxLength(1024), Required]
        public string Name { get; set; }

        [MaxLength(256)]
        public string Addition1 { get; set; }

        [MaxLength(256)]
        public string Addition2 { get; set; }

        [MaxLength(256)]
        public string Street { get; set; }

        [MaxLength(256)]
        public string Number { get; set; }

        [MaxLength(10), Required]
        public string Zip { get; set; }

        [MaxLength(256), Required]
        public string City { get; set; }

        public int BillingProfileId { get; set; }

        [ForeignKey(nameof(BillingProfileId))]
        public virtual TBillingProfile BillingProfile { get; set; }
    }
}
