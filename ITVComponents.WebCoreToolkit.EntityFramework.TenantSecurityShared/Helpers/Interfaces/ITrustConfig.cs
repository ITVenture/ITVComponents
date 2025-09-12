using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces
{
    public interface ITrustConfig<TTrustConfig> where TTrustConfig: ITrustConfig<TTrustConfig>
    {
        IDictionary<string, bool> SpecialFilterSettings { get; set; }

        TTrustConfig ApplySpecialFilters(TTrustConfig other)
        {
            if (SpecialFilterSettings != null && other.SpecialFilterSettings != null)
            {
                foreach (var name in SpecialFilterSettings.Keys.ToArray())
                {
                    if (other.SpecialFilterSettings.TryGetValue(name, out var setting))
                    {
                        SpecialFilterSettings[name] = SpecialFilterSettings[name] && setting;
                    }
                    else
                    {
                        SpecialFilterSettings[name] = false;
                    }
                }
            }
            else
            {
                SpecialFilterSettings = new Dictionary<string, bool>();
            }

            return Clone(other);
        }

        protected TTrustConfig Clone(TTrustConfig other);
    }
}
