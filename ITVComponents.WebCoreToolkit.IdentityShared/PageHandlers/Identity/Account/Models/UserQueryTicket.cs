using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models
{
    public class UserQueryTicket
    {
        public Guid UserResultId { get; set; }
        public bool UserExists { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool HasPassword { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string Email { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsAuthenticatorConfigured { get; set; }
        public bool MachineRememberForTwoFactor { get; set; }
        public int RecoveryCodesLeft { get; set; }
    }
}
