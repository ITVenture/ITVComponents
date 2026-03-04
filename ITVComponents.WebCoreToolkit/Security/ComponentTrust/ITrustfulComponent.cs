using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Security.PermissionFlagging;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    public interface ITrustfulComponent<TTrustConfig>:ITrustfulComponent where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
    {
        protected Stack<IFullSecurityAccessHelper<TTrustConfig>> securityStateStack { get; }

        protected IDictionary<string,bool> ComponentSpecialTrusts { get; }
        protected static void CheckSecurityRollbackObject(IFullSecurityAccessHelper<TTrustConfig> fullSecurityAccessHelper)
        {
            
        }

        public void RegisterSecurityRollback(
            IFullSecurityAccessHelper<TTrustConfig> fullSecurityAccessHelper)
        {
            if (!fullSecurityAccessHelper.CreatedWithContext)
            {
                throw new InvalidOperationException("Use Constructor with context argument, to use this method.");
            }

            securityStateStack.Push(fullSecurityAccessHelper.GetReverse(GetReverseTrust(fullSecurityAccessHelper.DesiredTrust)));

            ApplyTrust(fullSecurityAccessHelper.DesiredTrust);
        }

        public void RollbackSecurity(IFullSecurityAccessHelper<TTrustConfig> fullSecurityAccessHelper)
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

        bool ITrustfulComponent.IsComponentSecureFor(string topic)
        {
            return ComponentSpecialTrusts != null && ComponentSpecialTrusts.TryGetValue(topic, out var retVal) &&
                   retVal;
        }

        protected void ApplyTrust(TTrustConfig trust);

        protected TTrustConfig GetReverseTrust(TTrustConfig forwardTrustConfig);
    }
}
