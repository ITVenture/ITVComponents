using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Security;

namespace ITVComponents.Formatting.CustomFormat.Impl
{
    public class StringDecryptor:ICustomFormatter
    {
        public string ApplyFormat(string name, object rawValue, Func<string, string, string, object> argumentsCallback)
        {
            var pass = argumentsCallback(name, "decrypt", "password");
            string retVal = rawValue?.ToString();
            if (!string.IsNullOrEmpty(retVal))
            {
                bool success = false;
                if (pass is string pwd && !string.IsNullOrEmpty(pwd))
                {
                    try
                    {
                        retVal = AesEncryptor.Decrypt(retVal, Convert.FromBase64String(pwd));
                        success = true;
                    }
                    catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
                    {
                        // Expected when the value was encrypted with the application key: it is tried below.
                        LogEnvironment.LogDebugEvent($"Value '{name}' could not be decrypted with the given key, trying the application key: {ex.OutlineException()}", LogSeverity.Report);
                    }
                }
                else if (pass is byte[] pwb && pwb.Length != 0)
                {
                    try
                    {
                        retVal = AesEncryptor.Decrypt(retVal, pwb);
                        success = true;
                    }
                    catch (System.Security.Cryptography.CryptographicException ex)
                    {
                        // Expected when the value was encrypted with the application key: it is tried below.
                        LogEnvironment.LogDebugEvent($"Value '{name}' could not be decrypted with the given key, trying the application key: {ex.OutlineException()}", LogSeverity.Report);
                    }
                }

                if (!success)
                {
                    retVal = retVal.Decrypt();
                }

                return retVal;
            }

            return null;
        }

        public string Hint { get; } = "decrypt";
    }
}
