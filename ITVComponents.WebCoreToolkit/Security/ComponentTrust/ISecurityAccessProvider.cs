using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    public interface ISecurityAccessProvider
    {
        IFullSecurityAccessHelper<TTrustConfig> CreateForCaller<TTrustConfig, T>(T trustingObject, TTrustConfig desiredTrust = null)
            where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new();
    }
}
