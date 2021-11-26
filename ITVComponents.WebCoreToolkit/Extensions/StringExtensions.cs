using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Helpers;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class StringExtensions
    {
        public static string Translate(this string original, string jsonLanguageRecord)
        {
            if (!string.IsNullOrEmpty(jsonLanguageRecord) && !string.IsNullOrEmpty(original) && original.StartsWith("{") && original.EndsWith("}"))
            {
                try
                {
                    var ok = false;
                    var op = JsonHelper.FromJsonString<Dictionary<string, string>>(original);
                    if (op.ContainsKey(jsonLanguageRecord))
                    {
                        original = op[jsonLanguageRecord];
                        ok = true;
                    }

                    if (!ok && jsonLanguageRecord.Contains("-"))
                    {
                        var lg = jsonLanguageRecord.Substring(0, jsonLanguageRecord.IndexOf("-"));
                        if (op.ContainsKey(lg))
                        {
                            original = op[lg];
                            ok = true;
                        }
                    }

                    if (!ok && op.ContainsKey("Default"))
                    {
                        original = op["Default"];
                    }
                }
                catch
                {
                }
            }

            return original;
        }
    }
}
