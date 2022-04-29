using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Security;

namespace ITVComponents.Formatting.CustomFormat.Impl
{
    public class StringDecryptor:ICustomFormatter
    {
        public string ApplyFormat(string name, object rawValue, Func<string, string, string, object> argumentsCallback)
        {
            var pass = argumentsCallback(name, "decrypt", "password");
            var useRawKey = argumentsCallback(name, "decrypt", "useRawKey");
            var keyIsRaw = useRawKey is bool db && db;
            string retVal = rawValue?.ToString();
            if (!string.IsNullOrEmpty(retVal))
            {
                bool success = false;
                if (pass is string pwd && !string.IsNullOrEmpty(pwd))
                {
                    try
                    {
                        retVal = AesEncryptor.Decrypt(retVal, Convert.FromBase64String(pwd), !keyIsRaw);
                        success = true;
                    }
                    catch
                    {
                    }
                }
                else if (pass is byte[] pwb && pwb.Length != 0)
                {
                    try
                    {
                        retVal = AesEncryptor.Decrypt(retVal, pwb, !keyIsRaw);
                        success = true;
                    }
                    catch
                    {
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
