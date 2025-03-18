using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreIdentityPages.Services.Options;
using ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Areas.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Helpers
{
    internal static class CobNavDefaults
    {
        public static bool ResolveCobViews(ManageNavPage page)
        {
            bool retVal = false;
            string laqn = typeof(IdentityMessages).AssemblyQualifiedName;
            if (page.NavTag.ToLower() == "mytenants")
            {
                page.LocalizerType = laqn;
                page.NavLinkId = "my-tenants";
                page.NavigationLinkText = "My Tenants";
                page.PageLink = "./MyTenants";
                retVal = true;
            }

            return retVal;
        }
    }
}
