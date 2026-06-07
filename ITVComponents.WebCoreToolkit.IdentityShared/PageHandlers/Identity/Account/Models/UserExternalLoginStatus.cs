using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models
{
    public class UserExternalLoginStatus
    {
        public bool Success { get; set; }
        public bool ErrorOnLoadExternalData { get; set; }
        public SignInResult AuthResult { get; set; }
        public IdentityResult IdentityResult { get; set; }
        public string LoginUserName { get; set; }
        public string ProviderName { get; set; }
        public string UserId { get; set; }
    }
}
