using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ITVComponents.WebCoreToolkit.EntityFramework.CustomerOnboarding.Models
{
    public abstract class Address
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
        public virtual CompanyInfo CompanyInfo { get; set; }
    }
}
