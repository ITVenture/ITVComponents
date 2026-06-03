using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(TemplateModuleTemplateMarkup), "base")]
    public class TemplateModuleTemplateMarkup
    {
        public string TemplateModuleName { get; set; }

        /// <summary>
        /// Name of the Feature required to enable this module. Resolved against the target system's Features by name.
        /// </summary>
        public string RequiredFeature { get; set; }

        public TemplateModuleConfiguratorTemplateMarkup[] Configurators { get; set; }

        public string[] Scripts { get; set; }
    }
}
