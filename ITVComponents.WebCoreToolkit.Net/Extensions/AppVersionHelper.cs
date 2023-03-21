using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Net.Extensions
{
    public static class AppVersionHelper
    {
        public static string Version { get; private set; }

        private static string urlEncodeVersion;

        public static string UrlEncodedVersion => urlEncodeVersion??= WebEncoders.Base64UrlEncode(Encoding.Default.GetBytes(Version));

        public static void SetVersion(string version)
        {
            if (!string.IsNullOrEmpty(version) && string.IsNullOrEmpty(Version))
            {
                Version = version;
            }
            else
            {
                if (string.IsNullOrEmpty(version))
                {
                    throw new ArgumentException("Version must not be null", nameof(version));
                }

                throw new InvalidOperationException("Version can only be set once.");
            }
        }

        public static string ExtendUrlWithVersion(this string url)
        {
            var verTail = "";
            if (!string.IsNullOrEmpty(Version))
            {
                var codeVersion = UrlEncodedVersion;
                if (!string.IsNullOrEmpty(url))
                {
                    if (!url.Contains("?"))
                    {
                        verTail = $"?v={codeVersion}";
                    }
                    else
                    {
                        verTail = $"&v={codeVersion}";
                    }
                }
            }

            return $"{url}{verTail}";
        }
    }
}
