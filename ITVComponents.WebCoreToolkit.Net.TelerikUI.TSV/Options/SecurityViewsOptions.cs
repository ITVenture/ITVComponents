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
    }

    public enum LinkMode
    {
        MultiSelect,
        SubGrid
    }
}
