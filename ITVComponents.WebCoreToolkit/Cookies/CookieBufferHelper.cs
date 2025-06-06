using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ITVComponents.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Cookies
{
    public static class CookieBufferHelper
    {
        private const string ServerCookiePattern = "#IWC\\$\\${0}#(?<Guid>\\w+):(?<Validity>\\d{{17}})#/IWC\\$\\${0}#";

        private const string ServerCookieWritePattern =
            "#IWC$${0}#{1:N}:{2:yyyyMMddHHmmssfff}#/IWC$${0}#";

        public static bool IsBufferCookie(string cookieValue, string bufferTopic, out Guid guid, out DateTime validity, bool isEncrypted = true)
        {
            if (isEncrypted)
            {
                try
                {
                    cookieValue = Encoding.Default.GetString(WebEncoders.Base64UrlDecode(cookieValue).Decrypt());
                }
                catch { }
            }

            var topicPattern = string.Format(ServerCookiePattern, bufferTopic);
            Match rxm;
            if ((rxm = Regex.Match(cookieValue, topicPattern)).Success)
            {
                guid = Guid.ParseExact(rxm.Groups["Guid"].Value, "N");
                validity = DateTime.ParseExact(rxm.Groups["Validity"].Value, "yyyyMMddHHmmssfff",
                    CultureInfo.InvariantCulture);
                return true;
            }

            guid = Guid.Empty;
            validity = DateTime.MinValue;
            return false;
        }

        public static string CreateBufferCookieTag(string bufferTopic, CookieOptions cookieOptions, int validDays, out DateTime validity, out Guid guid, bool encrypt = true)
        {
            guid = Guid.NewGuid();
            validity = CalculateValidity(cookieOptions, validDays);
            var retVal  = string.Format(ServerCookieWritePattern, bufferTopic, guid, validity);
            if (encrypt)
            {
                retVal = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(retVal).Encrypt());
            }
            
            return retVal;
        }

        private static DateTime CalculateValidity(CookieOptions cookieOptions, int validDays)
        {
            var nw = DateTime.Now;
            DateTime validity = new DateTime(nw.Year, nw.Month, nw.Day, nw.Hour, nw.Minute, nw.Second, nw.Millisecond,
                DateTimeKind.Local).ToUniversalTime();
            if (cookieOptions != null)
            {
                var ts = TimeSpan.Zero;
                if (cookieOptions.MaxAge != null)
                {
                    ts = cookieOptions.MaxAge.Value;
                }
                else if (cookieOptions.Expires != null)
                {
                    ts = cookieOptions.Expires.Value.Subtract(nw);
                }

                if (ts != TimeSpan.Zero && ts.Microseconds != 0)
                {
                    ts = new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
                }

                if (ts != TimeSpan.Zero)
                {
                    validity = validity.Add(ts);
                }
                else
                {
                    validity = validity.AddDays(validDays);
                    cookieOptions.MaxAge = TimeSpan.FromDays(validDays);
                }
            }
            else
            {
                validity = validity.AddDays(validDays);
            }

            return validity;
        }
    }
}
