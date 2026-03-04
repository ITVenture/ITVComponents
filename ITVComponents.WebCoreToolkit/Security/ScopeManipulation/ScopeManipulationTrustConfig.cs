using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;

namespace ITVComponents.WebCoreToolkit.Security.ScopeManipulation
{
    public class ScopeManipulationTrustConfig:ITrustConfig<ScopeManipulationTrustConfig>
    {
        public bool SetExplicitScope { get; set; }
        public IDictionary<string, bool> SpecialFilterSettings { get; set; }
        public ScopeManipulationTrustConfig Clone(ScopeManipulationTrustConfig other)
        {
            var retVal = new ScopeManipulationTrustConfig { SetExplicitScope = SetExplicitScope };

            if (SpecialFilterSettings != null)
            {
                retVal.SpecialFilterSettings =
                    new Dictionary<string, bool>(
                        SpecialFilterSettings.Select(n => new KeyValuePair<string, bool>(n.Key, n.Value)));
            }

            return retVal;
        }
    }
}
