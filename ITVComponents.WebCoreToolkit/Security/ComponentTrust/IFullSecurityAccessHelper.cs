using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    public interface IFullSecurityAccessHelper:IDisposable
    {
        bool CreatedWithContext { get; }
        ITrustConfig DesiredTrust { get; }
        IFullSecurityAccessHelper ForwardHelper { get; }

        IFullSecurityAccessHelper GetReverse(ITrustConfig trustConfig);
    }

    public interface IFullSecurityAccessHelper<TTrustConfig>:IFullSecurityAccessHelper where TTrustConfig : class, ITrustConfig<TTrustConfig>
    {
        bool CreatedWithContext { get; }
        TTrustConfig DesiredTrust { get; }
        IFullSecurityAccessHelper<TTrustConfig> ForwardHelper { get; set; }

        IFullSecurityAccessHelper<TTrustConfig> GetReverse(TTrustConfig trustConfig);
    }
}
