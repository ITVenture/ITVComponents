using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces
{
    public interface ITrustfulComponent<TTrustConfig> where TTrustConfig : class, new()
    {
        protected Stack<FullSecurityAccessHelper<TTrustConfig>> securityStateStack { get; }
        protected static void CheckSecurityRollbackObject(FullSecurityAccessHelper<TTrustConfig> fullSecurityAccessHelper)
        {
            
        }

        protected internal void RegisterSecurityRollback(
            FullSecurityAccessHelper<TTrustConfig> fullSecurityAccessHelper)
        {
            if (!fullSecurityAccessHelper.CreatedWithContext)
            {
                throw new InvalidOperationException("Use Constructor with context argument, to use this method.");
            }

            securityStateStack.Push(new FullSecurityAccessHelper<TTrustConfig>
            {
                ForwardHelper = fullSecurityAccessHelper,
                DesiredTrust = GetReverseTrust(fullSecurityAccessHelper.DesiredTrust)
            });

            ApplyTrust(fullSecurityAccessHelper.DesiredTrust);
        }

        protected internal void RollbackSecurity(FullSecurityAccessHelper<TTrustConfig> fullSecurityAccessHelper)
        {
            var tmp = securityStateStack.Pop();
            if (tmp.ForwardHelper == fullSecurityAccessHelper)
            {
                ApplyTrust(tmp.DesiredTrust);
            }
            else
            {
                throw new InvalidOperationException("Invalid Disposal-order!");
            }
        }

        protected void ApplyTrust(TTrustConfig trust);

        protected TTrustConfig GetReverseTrust(TTrustConfig forwardTrustConfig);
    }
}
