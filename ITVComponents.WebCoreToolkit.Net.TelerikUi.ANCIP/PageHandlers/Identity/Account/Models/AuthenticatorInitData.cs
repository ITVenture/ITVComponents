using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models
{
    public class AuthenticatorInitData
    {
        public string SharedKey { get; set; }

        public string AuthenticatorUri { get; set; }
        public bool Success { get; set; }
    }
}
