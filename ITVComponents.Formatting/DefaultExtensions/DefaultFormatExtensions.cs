using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Security;

namespace ITVComponents.Formatting.DefaultExtensions
{
    internal static class DefaultFormatExtensions
    {
        public static void RegisterDefaultFormatExtensions()
        {
            TextFormat.AddCustomFormatHint("kv", FormatKeyValue);
            TextFormat.AddCustomFormatHint("lo", n => n?.ToString()?.ToLower());
            TextFormat.AddCustomFormatHint("hi", n => n?.ToString()?.ToUpper());
            TextFormat.AddCustomFormatHint("decrypt", n => PasswordSecurity.Decrypt(n?.ToString()));
        }

        private static string FormatKeyValue(object target)
        {
            string[] keys;
            object[] values;
            IBasicKeyValueProvider ikvp = target as IBasicKeyValueProvider;
            IDictionary<string, object> isod = target as IDictionary<string, object>;
            if (ikvp != null)
            {
                keys = ikvp.Keys.ToArray();
                values = (from t in keys select ikvp[t]).ToArray();
            }
            else if (isod != null)
            {
                keys = isod.Keys.ToArray();
                values = (from t in keys select isod[t]).ToArray();
            }
            else
            {
                return target?.ToString();
            }

            if (keys.Length != 0)
            {
                int mx = keys.Max(n => n.Length);
                return string.Join("\r\n", from k in keys.Select((s, i) => new {Index = i, Key = s})
                    join v in values.Select((v, i) => new {Index = i, Value = v}) on k.Index equals v.Index
                    orderby k.Key
                    select string.Format($"{{0,-{mx}}} = {{1}}", k.Key, v.Value));
            }

            return "";
        }
    }
}
