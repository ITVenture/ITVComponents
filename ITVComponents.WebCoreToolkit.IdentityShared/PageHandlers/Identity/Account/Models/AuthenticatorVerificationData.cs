using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models
{
    public class AuthenticatorVerificationData
    {
        public bool Success { get; set; }
        public string UserId { get; set; }
        public int RecoveryCodeCount { get; set; }
    }
}
