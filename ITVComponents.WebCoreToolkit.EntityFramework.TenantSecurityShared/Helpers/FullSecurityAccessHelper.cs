using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public sealed class FullSecurityAccessHelper<TTrustConfig>:IDisposable, IFullSecurityAccessHelper<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
    {
        public TTrustConfig DesiredTrust { get; set; }
        IFullSecurityAccessHelper IFullSecurityAccessHelper.ForwardHelper
        {
            get => ForwardHelper;
        }

        IFullSecurityAccessHelper IFullSecurityAccessHelper.GetReverse(ITrustConfig trustConfig)
        {
            return GetReverse((TTrustConfig)trustConfig);
        }

        public IFullSecurityAccessHelper<TTrustConfig> GetReverse(TTrustConfig reverseTrust)
        {
            return new FullSecurityAccessHelper<TTrustConfig>
            {
                ForwardHelper = this,
                DesiredTrust = reverseTrust
            };
        }

        private readonly ITrustfulComponent<TTrustConfig> trustfulTarget;
        ITrustConfig IFullSecurityAccessHelper.DesiredTrust { get; }
        public IFullSecurityAccessHelper<TTrustConfig> ForwardHelper { get; set; }

        public bool CreatedWithContext { get; }

        public FullSecurityAccessHelper()
        {
        }

        internal FullSecurityAccessHelper(ITrustfulComponent<TTrustConfig> trustfulComponent, TTrustConfig desiredTrust)
        {
            DesiredTrust = desiredTrust;
            trustfulTarget = trustfulComponent;
            CreatedWithContext = true;
            trustfulTarget.RegisterSecurityRollback(this);
        }

        public void Dispose()
        {
            trustfulTarget.RollbackSecurity(this);
        }
            
    }
}
