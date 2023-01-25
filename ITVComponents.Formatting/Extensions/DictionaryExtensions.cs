using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Scripting.CScript.ScriptValues;

namespace ITVComponents.Formatting.Extensions
{
    public static class DictionaryExtensions
    {
        public static Dictionary<string, object> ExtendDictionary(this Dictionary<string, object> source,
            Dictionary<string, object> extendee)
        {
            var retVal = extendee.Count == 0?extendee:new Dictionary<string, object>();
            if (retVal != extendee)
            {
                foreach (var kvp in extendee)
                {
                    retVal.Add(kvp.Key,kvp.Value);
                }
            }

            foreach (var key in source.Keys)
            {
                if (!retVal.ContainsKey(key))
                {
                    retVal.Add(key, source[key]);
                }
            }

            return retVal;
        }
    }
}
