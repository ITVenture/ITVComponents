using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Json;

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
                    var op = JsonHelper.FromJsonString<Dictionary<string, string>>(original, SerializationTypingMode.StaticTyping);
                    original = op.Translate(jsonLanguageRecord, original);
                }
                catch
                {
                }
            }

            return original;
        }
    }
}
