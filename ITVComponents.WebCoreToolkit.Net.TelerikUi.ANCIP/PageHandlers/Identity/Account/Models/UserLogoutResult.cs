using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models
{
    public class UserLogoutResult
    {
        public string UserId { get; set; }

        public bool LoggedOut { get; set; }

        public bool UserDeleted { get; set; }
        public IdentityResult IdentityResult { get; set; }
    }
}
