using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AdminViews.TenantSecurityViews.ViewModel
{
    public class ExternalServiceTestDataViewModel
    {
        public int OAuthServiceId { get; set; }

        public int ActionTypeId { get; set; }

        public string TargetUrl { get; set; }

        public string HttpActionBody { get; set; }
        public string ActionBodyContentType { get; set; }

        public string CustomHeaders { get; set; }
    }
}
