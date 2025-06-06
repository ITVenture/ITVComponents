using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ITVComponents.Json;
using ITVComponents.Scripting.CScript.ScriptValues;
using ITVComponents.Security;
using Microsoft.AspNetCore.WebUtilities;

namespace ITVComponents.WebCoreToolkit.Tokens
{
    public static class TokenHelper
    {
        public static string CompressToken<T>(this T token, bool forUrl = true, bool encrypt=true, string password = null, bool useHighCompression = false)
        {
            using var mst = new MemoryStream();
            using (var dst = new DeflateStream(mst, !useHighCompression?CompressionLevel.Optimal:CompressionLevel.SmallestSize))
            {
                JsonHelper.WriteObject(token, SerializationTypingMode.StaticTyping, dst);
            }

            byte[] ret = mst.ToArray();
            if (encrypt)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    ret = ret.Encrypt(password);
                }
                else
                {
                    ret = ret.Encrypt();
                }
            }

            if (forUrl)
            {
                return $"{(encrypt?"c.":"")}{WebEncoders.Base64UrlEncode(ret)}";
            }

            return $"{(encrypt?"c.":"")}{Convert.ToBase64String(ret)}";
        }

        public static T DecompressToken<T>(this string compressedToken, string password = null)
        {
            var encrypted = compressedToken.StartsWith("c.");
            var str = encrypted ? compressedToken.Substring(2) : compressedToken;
            byte[] rawDoc = str.IndexOfAny(new[] {'-', '_'}) == -1 ? Convert.FromBase64String(str) : WebEncoders.Base64UrlDecode(str);
            if (encrypted)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    rawDoc = rawDoc.Decrypt(password);
                }
                else
                {
                    rawDoc = rawDoc.Decrypt();
                }
            }

            using (MemoryStream mst = new MemoryStream(rawDoc))
            {
                using (DeflateStream dfs = new DeflateStream(mst, CompressionMode.Decompress))
                {
                    return JsonHelper.ReadObject<T>(dfs, SerializationTypingMode.StaticTyping);
                }
            }
        }
    }
}
