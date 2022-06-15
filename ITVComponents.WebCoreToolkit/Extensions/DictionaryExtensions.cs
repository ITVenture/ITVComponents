using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class DictionaryExtensions
    {
        public static T Translate<T>(this IDictionary<string, T> dic, string language, T defaultValue = default(T))
        {
            var retVal = defaultValue;
            bool ok = false;
            if (dic.ContainsKey(language))
            {
                retVal = dic[language];
                ok = true;
            }

            if (!ok && language.Contains("-"))
            {
                var lg = language.Substring(0, language.IndexOf("-"));
                if (dic.ContainsKey(lg))
                {
                    retVal = dic[lg];
                    ok = true;
                }
            }

            if (!ok && dic.ContainsKey("Default"))
            {
                retVal = dic["Default"];
            }

            return retVal;
        }
    }
}
