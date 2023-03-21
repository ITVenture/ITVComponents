using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.Options
{
    public class NetPartOptions
    {
        public string TenantParam { get; set; }
        
        public bool WithTenants { get; set; }

        public bool WithoutTenants { get; set; }

        public bool WithAreas { get;set; }

        public bool WithoutAreas { get; set; } = true;

        public bool WithSecurity { get; set; } = true;

        public bool WithoutSecurity { get; set; } = false;

        public bool UseAutoForeignKeys { get; set; } = false;
        public bool UseDiagnostics { get; set; } = false;
        public bool UseWidgets { get; set; } = false;
        public bool ExposeFileSystem { get; set; } = false;
        public bool ExposeClientSettings { get; set; } = false;
        public bool UseFileServices { get; set; } = false;
        public bool UseTenantSwitch { get; set; } = false;

        public string UrlQueryExtVersion { get; set; } = null;
    }
}
