using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.Options
{
    public class SecurityViewsOptions
    {
        public LinkMode TenantLinkMode { get; set; } = LinkMode.MultiSelect;

        public bool UseExplicitTenantPasswords { get; set; } = false;

        public string TenantParam { get; set; }
        public bool UseModuleTemplates { get; set; } = false;

        public bool WithTenants { get; set; }

        public bool WithoutTenants { get; set; }

        public bool WithAreas { get; set; }

        public bool WithoutAreas { get; set; }
    }

    public enum LinkMode
    {
        MultiSelect,
        SubGrid
    }
}
