using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Formatting;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.CoreIdentity.Models;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.COB.Extensions
{
    public static class SignInManagerExtensions
    {
        public static async Task<AuthenticationHandlerDefinition[]> GetAuthenticationHandlerDefinitions<TUser>(this SignInManager<TUser> signInManager, string logoPattern, IEnumerable<AuthenticationHandlerDefinition> rawDefinitions, Func<AuthenticationScheme, bool> filter = null) where TUser:class
        {
            var tmp = (await signInManager.GetExternalAuthenticationSchemesAsync());
            if (filter != null)
            {
                tmp = tmp.Where(filter);
            }

            var l = tmp.ToList();
            //var optionsValue = availableAuthenticators.Value;
            //var logoPattern = optionsValue.LogoPattern;
            var providers = (from t in l
                             join a in rawDefinitions
                    on t.Name equals a.AuthenticationSchemeName into j
                from c in j.DefaultIfEmpty()
                where c?.DisplayInHandlerSelection ?? true
                select c ?? new AuthenticationHandlerDefinition
                {
                    AuthenticationSchemeName = t.Name,
                    DisplayInHandlerSelection = true,
                    DisplayName = t.DisplayName,
                    LogoFile = t.FormatText(logoPattern, TextFormat.DefaultFormatPolicyWithPrimitives)// $"/images/logo/login-logo{t.DisplayName}.png"
                });
            return providers.ToArray();
        }
    }
}