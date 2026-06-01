using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class AuthenticationTypeViewModel
    {
        [Key]
        public int AuthenticationTypeId { get; set; }

        [Required, MaxLength(512)]
        public string AuthenticationTypeName { get; set; }
    }
}
