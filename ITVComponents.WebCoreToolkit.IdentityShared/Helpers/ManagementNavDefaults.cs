using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Annotations;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity;
using ITVComponents.WebCoreToolkit.IdentityShared.Options;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Options;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Helpers
{
    public static class ManagementNavDefaults
    {
        private static List<Func<ManageNavPage, bool>> resolverCallbacks = new List<Func<ManageNavPage, bool>>();

        public static void RegisterNavTagResolverCallback(Func<ManageNavPage, bool> callback)
        {
            lock (resolverCallbacks)
            {
                resolverCallbacks.Add(callback);
            }
        }

        public static ManageNavOptions AddDefaultPages(this ManageNavOptions navOptions, bool useExternalLogin)
        {
            navOptions.AddNavPage(GetDefaultPage("index"));
            navOptions.AddNavPage(GetDefaultPage("email"));
            navOptions.AddNavPage(GetDefaultPage("changePassword"));
            if (useExternalLogin)
            {
                navOptions.AddNavPage(GetDefaultPage("externalLogins"));
            }

            navOptions.AddNavPage(GetDefaultPage("twoFactor"));
            navOptions.AddNavPage(GetDefaultPage("personalData"));
            return navOptions;
        }

        public static ManageNavPage FromNavPageDefinition(ManagementNavPageDefinition webPartConfig)
        {
            var retVal = GetDefaultPage(webPartConfig.NavTag);
            if (!string.IsNullOrEmpty(webPartConfig.LocalizerType))
            {
                retVal.LocalizerType = webPartConfig.LocalizerType;
            }

            if (!string.IsNullOrEmpty(webPartConfig.NavLinkId))
            {
                retVal.NavLinkId= webPartConfig.NavLinkId;
            }

            if (!string.IsNullOrEmpty(webPartConfig.NavigationLinkText))
            {
                retVal.NavigationLinkText= webPartConfig.NavigationLinkText;
            }

            if (!string.IsNullOrEmpty(webPartConfig.PageLink))
            {
                retVal.PageLink= webPartConfig.PageLink;
            }

            return retVal;
        }

        public static ManageNavPage GetDefaultPage(string tag)
        {
            string idmAssembly = typeof(IdentityMessages).AssemblyQualifiedName;
            ManageNavPage retVal = new ManageNavPage { NavTag = tag };
            bool resolved = false;
            switch (tag.ToLower())
            {
                case "index":
                    retVal.LocalizerType = idmAssembly;
                    retVal.NavLinkId = "profile";
                    retVal.NavigationLinkText = "Profile";
                    retVal.PageLink = "./Index";
                    resolved = true;
                    break;
                case "email":
                    retVal.LocalizerType = idmAssembly;
                    retVal.NavLinkId = "email";
                    retVal.NavigationLinkText = "Email";
                    retVal.PageLink = "./Email";
                    resolved = true;
                    break;
                case "changepassword":
                    retVal.LocalizerType = idmAssembly;
                    retVal.NavLinkId = "change-password";
                    retVal.NavigationLinkText = "Password";
                    retVal.PageLink = "./ChangePassword";
                    resolved = true;
                    break;
                case "externallogins":
                    retVal.LocalizerType = idmAssembly;
                    retVal.NavLinkId = "external-login";
                    retVal.NavigationLinkText = "External logins";
                    retVal.PageLink = "./ExternalLogins";
                    resolved = true;
                    break;
                case "twofactor":
                    retVal.LocalizerType = idmAssembly;
                    retVal.NavLinkId = "two-factor";
                    retVal.NavigationLinkText = "Two-factor authentication";
                    retVal.PageLink = "./TwoFactorAuthentication";
                    resolved = true;
                    break;
                case "personaldata":
                    retVal.LocalizerType = idmAssembly;
                    retVal.NavLinkId = "personal-data";
                    retVal.NavigationLinkText = "Personal data";
                    retVal.PageLink = "./PersonalData";
                    resolved = true;
                    break;
            }

            lock (resolverCallbacks)
            {
                for (int i = 0; !resolved && i < resolverCallbacks.Count; i++)
                {
                    resolved = resolverCallbacks[i](retVal);
                }
            }

            return retVal;
        }
    }
}
