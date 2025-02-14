using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.PageHandlers.Identity.Account.Manage.Impl
{
    internal class DownloadPersonalDataHandler: IDownloadPersonalDataHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(ClaimsPrincipal user)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return null;
        }

        public Task<IDictionary<string, string>> FetchPersonalData(UserQueryTicket user)
        {
            return Task.FromResult((IDictionary<string, string>)new Dictionary<string, string>());
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }
    }
}
