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

        /// <summary>The value; for a secret parameter always null when read, and empty on save means "keep".</summary>
        public string ParameterValue { get; set; }

        /// <summary>Whether the value is stored encrypted.</summary>
        public bool IsSecret { get; set; }

        /// <summary>Whether a value is stored - the only thing the view learns about a secret.</summary>
        public bool HasValue { get; set; }
    }
}
