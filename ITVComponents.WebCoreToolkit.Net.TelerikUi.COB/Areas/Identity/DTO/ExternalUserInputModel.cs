using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity.DTO
{
    public class ExternalUserInputModel
    {
        [Required]
        [EmailAddress(ErrorMessage = "ITV:DataTypeAttribute.EmailAddress_ValidationError")]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}
