using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class PersonalDataHandler: IPersonalDataHandler
    {
        public bool UsePage => false;
        public Task<bool> UserExists(ClaimsPrincipal user)
        {
            return Task.FromResult(false);
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }
    }
}
