using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models
{
    public class AuthenticationTypeClaimTemplateMarkup
    {
        public string AuthenticationTypeName { get; set; }

        public string IncomingClaimName { get; set; }

        public string Condition { get; set; }

        public string OutgoingClaimName { get; set; }

        public string OutgoingValueType { get; set; }

        public string OutgoingIssuer { get; set; }

        public string OutgoingOriginalIssuer { get; set; }

        public string OutgoingClaimValue { get; set; }
    }
}
