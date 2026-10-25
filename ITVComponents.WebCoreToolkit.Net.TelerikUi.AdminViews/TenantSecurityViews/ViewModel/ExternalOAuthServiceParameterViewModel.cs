using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class ExternalOAuthServiceParameterViewModel
    {
        public int OAuthServiceParameterId { get; set; }

        public int OAuthServiceId { get; set; }

        public string ParameterName { get; set; }

        public string ParameterValue { get; set; }
    }
}
