using System.Collections.Generic;
using System.Linq;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    public interface ITrustConfig
    {

    }
    public interface ITrustConfig<TTrustConfig>:ITrustConfig where TTrustConfig: ITrustConfig<TTrustConfig>
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
