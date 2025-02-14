using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Impl
{
    internal class LogoutHandler: ILogoutHandler
    {
        public bool UsePage => false;
        public Task SignOut()
        {
            return Task.CompletedTask;
        }
    }
}
