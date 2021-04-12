using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class CustomUserPropertyViewModel
    {
        [Key]
        public int CustomUserPropertyId { get; set; }

        [MaxLength(150),Required]
        public string PropertyName{get;set;}

        [MaxLength(1024),Required]
        public string Value { get; set; }
    }
}
