using System.Text.Json.Serialization;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.Models
{
    [JsonDerivedType(typeof(TemplateModuleConfiguratorParameterTemplateMarkup), "base")]
    public class TemplateModuleConfiguratorParameterTemplateMarkup
    {
        public string ParameterName { get; set; }

        public string DisplayName { get; set; }

        public string ParameterValue { get; set; }
    }
}
