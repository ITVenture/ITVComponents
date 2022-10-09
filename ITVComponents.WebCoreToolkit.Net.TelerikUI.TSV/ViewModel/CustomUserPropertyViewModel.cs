using System.ComponentModel.DataAnnotations;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class CustomUserPropertyViewModel
    {
        [Key]
        public int CustomUserPropertyId { get; set; }

        [MaxLength(150),Required]
        public string PropertyName{get;set;}

        [Required]
        public string Value { get; set; }

        public CustomUserPropertyType PropertyType { get; set; }
    }
}
