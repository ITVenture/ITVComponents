using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Models;

namespace ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl
{
    internal class ResendEmailConfirmationHandler:IResendEmailConfirmationHandler
    {
        public bool UsePage => false;
        public Task<UserQueryTicket> FetchUser(string email)
        {
            return Task.FromResult(new UserQueryTicket { UserExists = false });
        }

        public bool ReleaseUser(UserQueryTicket userTicket)
        {
            return true;
        }

        public Task<UserMailTokenData> GetMailToken(UserQueryTicket userTicket)
        {
            return Task.FromResult(new UserMailTokenData { Success = false });
        }
    }
}
