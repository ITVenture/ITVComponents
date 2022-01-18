using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.TenantSecurityViews.ViewModel
{
    public class AuthenticationClaimMappingViewModel
    {
        public int AuthenticationClaimMappingId { get; set; }

        public int AuthenticationTypeId { get; set; }

        [MaxLength(512), Required]
        public string IncomingClaimName { get; set; }

        public string Condition { get; set; }

        [MaxLength(512), Required]
        public string OutgoingClaimName { get; set; }

        [MaxLength(512)]
        public string OutgoingValueType { get; set; }

        [MaxLength(512)]
        public string OutgoingIssuer { get; set; }

        [MaxLength(512)]
        public string OutgoingOriginalIssuer { get; set; }

        [Required]
        public string OutgoingClaimValue { get; set; }
    }
}
