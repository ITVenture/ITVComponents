using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class WebPluginConstantViewModel
    {
        public int WebPluginConstantId { get; set; }

        [Required, MaxLength(128)]
        public string Name { get;set; }

        [Required]
        public string Value { get;set; }
        
        public int? TenantId { get; set; }
    }
}
