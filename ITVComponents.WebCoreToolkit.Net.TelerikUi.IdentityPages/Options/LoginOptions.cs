using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Options
{
    public class LoginOptions
    {
        public bool UserNameIsEmail { get; set; } = true;
        public bool UseLocalAccounts { get; set; }
        public UserRegistrationInfo RegistrationPage { get; set; }
        public ExternalLoginConfig ExternalLoginPage { get; set; }
    }
}
