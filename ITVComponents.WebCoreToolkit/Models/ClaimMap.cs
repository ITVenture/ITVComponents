using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.ExtendedFormatting;
using ITVComponents.Logging;

namespace ITVComponents.WebCoreToolkit.Models
{
    public class ClaimMap:IBasicKeyValueProvider
    {
        private readonly Dictionary<string, ClaimData[]> map;

        public ClaimMap(Dictionary<string, ClaimData[]> map)
        {
            this.map = map;
        }
        public object this[string name] {
            get
            {
                object retVal = null;
                var found = map.TryGetValue(name, out var value);
                if (found)
                {
                    if (value.Length == 1)
                    {
                        retVal = value[0];
                    }
                    else
                    {
                        retVal = value;
                    }
                }
                else
                {
                    LogEnvironment.LogEvent($"Requested Claim {name} was not found in this claim-Map.", LogSeverity.Warning);
                }


                return retVal;
            }
        }

        public string[] Keys => map.Keys.ToArray();
        public bool ContainsKey(string key)
        {
            return map.ContainsKey(key);
        }
    }
}
