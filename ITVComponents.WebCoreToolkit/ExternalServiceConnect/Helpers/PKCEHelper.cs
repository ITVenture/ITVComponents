using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.ExternalServiceConnect.Helpers
{
    public static class PkceHelper
    {
        public static string CreateCodeVerifier()
            => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        public static string CreateCodeChallenge(string verifier)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
            return WebEncoders.Base64UrlEncode(hash);
        }
    }
}
