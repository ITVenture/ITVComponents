using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.OnboardingShared.Models
{
    /// <summary>
    /// Abstract base for company addresses (default / invoice).
    /// Concrete subclasses bind <typeparamref name="TCompanyInfo"/> to the consumer-specific CompanyInfo derivative.
    /// </summary>
    public abstract class AddressBase<TCompanyInfo>
        where TCompanyInfo : class
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

        public int CompanyInfoId { get; set; }

        [ForeignKey(nameof(CompanyInfoId))]
        public virtual TCompanyInfo CompanyInfo { get; set; }
    }
}
