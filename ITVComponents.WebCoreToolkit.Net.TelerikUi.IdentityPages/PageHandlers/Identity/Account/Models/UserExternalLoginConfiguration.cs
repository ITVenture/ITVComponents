using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.PageHandlers.Identity.Account.Models
{
    public class UserExternalLoginConfiguration
    {
        public IList<UserLoginInfo> ExternalUserLogins { get; set; }
        public AuthenticationHandlerDefinition[] AvailableLogins { get; set; }
    }
}
