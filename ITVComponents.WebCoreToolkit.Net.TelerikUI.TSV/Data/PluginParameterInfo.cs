namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Data
{
    public class PluginParameterInfo
    {
        public string ParameterName { get; set; }
        
        public string ValueInputFormat { get; set; }

        public string ValueOutputFormat { get; set; }
        public ModuleParameterTemplate[] Inputs { get; set; }
    }
}
