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
        /// <summary>
        /// Ob dieser Browser fuer die Zwei-Faktor-Anmeldung gemerkt ist. <c>null</c> heisst NICHT "nein",
        /// sondern "nicht feststellbar": die Antwort steht in einem Cookie und braucht eine laufende Anfrage.
        /// Ausserhalb einer solchen - auf einem Blazor-Circuit - bleibt sie offen.
        /// </summary>
        public bool? MachineRememberForTwoFactor { get; set; }
        public int RecoveryCodesLeft { get; set; }
    }
}
