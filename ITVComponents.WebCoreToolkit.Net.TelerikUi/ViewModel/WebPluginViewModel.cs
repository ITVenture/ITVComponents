using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.ViewModel
{
    public class WebPluginViewModel
    {
        [Key]
        public int WebPluginId { get; set; }

        [MaxLength(50)]
        [Required]
        public string UniqueName { get; set; }

        [MaxLength(2048)]
        public string Constructor { get; set; }

        public bool AutoLoad { get; set; }

        [MaxLength(2048)]
        public string StartupRegistrationConstructor { get; set; }
        
        public int? TenantId { get;set; }
    }
}
