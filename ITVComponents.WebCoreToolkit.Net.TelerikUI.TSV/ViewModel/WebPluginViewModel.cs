using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class WebPluginViewModel
    {
        [Key]
        public int WebPluginId { get; set; }

        [MaxLength(300)]
        [Required]
        public string UniqueName { get; set; }

        [MaxLength(8192)]
        public string Constructor { get; set; }

        public bool AutoLoad { get; set; }

        [MaxLength(8192)]
        public string StartupRegistrationConstructor { get; set; }
        
        public int? TenantId { get;set; }
    }
}
