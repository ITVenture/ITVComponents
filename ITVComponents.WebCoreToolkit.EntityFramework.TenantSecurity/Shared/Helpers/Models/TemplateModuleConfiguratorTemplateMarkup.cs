using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(TemplateModuleConfiguratorTemplateMarkup), "base")]
    public class TemplateModuleConfiguratorTemplateMarkup
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string CustomConfiguratorView { get; set; }

        public string ConfiguratorTypeBack { get; set; }

        public TemplateModuleConfiguratorParameterTemplateMarkup[] Parameters { get; set; }
    }
}
