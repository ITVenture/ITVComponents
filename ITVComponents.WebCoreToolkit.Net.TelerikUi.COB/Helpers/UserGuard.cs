using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.EntityFramework.AspNetCoreTenants.Models;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Helpers;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Helpers
{
    internal class UserGuard:UserGuard<User>
    {
        public UserGuard(SignInManager<User> signInManager, UserManager<User> userManager) : base(signInManager, userManager)
        {
        }

        public override string GetUserId(User user)
        {
            return user.Id;
        }
    }
}
