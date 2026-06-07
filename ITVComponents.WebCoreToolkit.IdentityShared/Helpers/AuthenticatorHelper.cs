using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.IdentityShared.Helpers
{
    public static class AuthenticatorHelper
    {
        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        public static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition, 4)).Append(" ");
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition));
            }

            return result.ToString().ToLowerInvariant();
        }

        public static string GenerateQrCodeUri(string email, string productTag, string unformattedKey, UrlEncoder encoder)
        {
            return string.Format(
            AuthenticatorUriFormat,
                encoder.Encode(productTag),
                encoder.Encode(email),
                unformattedKey);
        }
    }
}
