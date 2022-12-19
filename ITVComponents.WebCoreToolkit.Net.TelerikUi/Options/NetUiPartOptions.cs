namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.Options
{
    public class NetUiPartOptions
    {
        public string TenantParam { get; set; }
        
        public bool WithTenants { get; set; }

        public bool WithoutTenants { get; set; }

        public bool WithAreas { get;set; }

        public bool WithoutAreas { get; set; } = true;

        public bool WithSecurity { get; set; } = true;

        public bool WithoutSecurity { get; set; } = false;

        public bool UseFilteredForeignKeys { get; set; } = false;
        public bool UseViews { get; set; } = false;
        public bool UseValidationAdapters { get; set; } = false;

        public bool UseScriptLocalization { get; set; } = false;
        public string? LayoutPage { get; set; }
        public bool UseHealthView { get; set; } = false;
        public bool RegisterHub { get; set; }
    }
}
