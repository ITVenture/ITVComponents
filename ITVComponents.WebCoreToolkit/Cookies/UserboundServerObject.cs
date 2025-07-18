using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.Security;
using ITVComponents.WebCoreToolkit.Tokens;
using Microsoft.AspNetCore.Http;

namespace ITVComponents.WebCoreToolkit.Cookies
{
    public class UserboundServerObject<T>
        where T : class, new()
    {
        private readonly string cookieName;
        private readonly TimeSpan durability;
        private readonly ICookieService cookieService;
        private readonly IContextUserProvider userProvider;
        private readonly bool preserveObjectReferences;
        private UserDataContainer<T> data;

        public UserboundServerObject(string cookieName, TimeSpan durability, ICookieService cookieService, IContextUserProvider userProvider, bool preserveObjectReferences = false)
        {
            this.cookieName = cookieName;
            this.durability = durability;
            this.cookieService = cookieService;
            this.userProvider = userProvider;
            this.preserveObjectReferences = preserveObjectReferences;
        }

        public T Data => (data??=ReadCookieData())?.Data;

        private UserDataContainer<T> ReadCookieData()
        {
            var userName = userProvider.User.Identity?.Name;
            if (cookieService.Ready && userName != null)
            {
                if (cookieService.TryGetCookie(cookieName, out var rawData))
                {
                    try
                    {
                        var retVal = rawData.DecompressToken<UserDataContainer<T>>(preserveObjectReferences:preserveObjectReferences);
                        if (retVal.ExpectedUserName == userName)
                        {
                            return retVal;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"Failed to deserialize cookie {cookieName} for {userName}. {ex.Message}.", LogSeverity.Warning);
                    }
                }

                //if no cookie is found, or the userName does not match, we create a new instance
                return new UserDataContainer<T> { Data = new T(), ExpectedUserName = userName };
            }

            return null;
        }

        public void Save()
        {
            if (data != null)
            {
                var json = data.CompressToken(false, preserveObjectReferences:preserveObjectReferences);
                cookieService.SetCookie(cookieName, json, new CookieOptions { MaxAge = durability },
                    CookieStrategy.Server);
            }
        }
    }
}
