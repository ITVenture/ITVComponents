using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.EntityFramework.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.DTO
{
    public class CompanyInputModel
    {
        [Required, MaxLength(256), Display(Name = "Company E-Mail"), EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
        public string Email { get; set; }

        [Required]
        [StringLength(100/*, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long."*/, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Administrator-Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Administrator-password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }


        [MaxLength(100), Display(Name = "Company Phone"), Phone(ErrorMessage = "ITV:DataTypeAttribute.PhoneNumber")]
        public string PhoneNumber { get; set; }


        [MaxLength(1024), Required, Display(Name = "Company Name")]
        public string Name { get; set; }

        [MaxLength(256), Display(Name = "Addition1")]
        public string Addition1 { get; set; }

        [MaxLength(256), Display(Name = "Addition2")]
        public string Addition2 { get; set; }

        [MaxLength(256), Display(Name = "Street")]
        public string Street { get; set; }

        [MaxLength(256), Display(Name = "Number")]
        public string Number { get; set; }

        [MaxLength(10), Required, Display(Name = "Zip")]
        public string Zip { get; set; }

        [MaxLength(256), Required, Display(Name = "City")]
        public string City { get; set; }

        [MaxLength(1024), Display(Name = "TimeZone")]
        public string TimeZone { get; set; }

        [Display(Name = "UseInvoiceAddr")]
        public bool UseInvoiceAddr { get; set; }

        [ConditionalRequired(BackEndCondition = "UseInvoiceAddr", ClientCondition = "ITVenture.Pages.Identity.Register.RequireInvoiceFields", ErrorMessage = "RequiredForInvoiceAddr")]
        [Display(Name = "Invoicee Name")]
        public string InvoiceName { get; set; }

        [MaxLength(256), Display(Name = "Addition1")]
        public string InvoiceAddition1 { get; set; }

        [MaxLength(256), Display(Name = "Addition2")]
        public string InvoiceAddition2 { get; set; }

        [MaxLength(256), Display(Name = "Street")]
        public string InvoiceStreet { get; set; }

        [MaxLength(256), Display(Name = "Number")]
        public string InvoiceNumber { get; set; }

        [MaxLength(10), Display(Name = "Zip")]
        [ConditionalRequired(BackEndCondition = "UseInvoiceAddr", ClientCondition = "ITVenture.Pages.Identity.Register.RequireInvoiceFields", ErrorMessage = "RequiredForInvoiceAddr")]
        public string InvoiceZip { get; set; }

        [MaxLength(256), Display(Name = "City")]
        [ConditionalRequired(BackEndCondition = "UseInvoiceAddr", ClientCondition = "ITVenture.Pages.Identity.Register.RequireInvoiceFields", ErrorMessage = "RequiredForInvoiceAddr")]
        public string InvoiceCity { get; set; }

        [ForceTrue(ErrorMessage = "Must accept the terms of Service."), Display(Name = "AcceptTOS")]
        public bool AcceptTOS { get; set; }
    }
}
