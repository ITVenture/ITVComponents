using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Helpers;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.IdentityPages.Services.Impl
{
    internal class IdentityUserGuard<TUser>:UserGuard<TUser> where TUser:IdentityUser
    {
        public IdentityUserGuard(SignInManager<TUser> signInManager, UserManager<TUser> userManager) : base(signInManager, userManager)
        {
        }

        public override string GetUserId(TUser user)
        {
            return user.Id;
        }
    }
}
