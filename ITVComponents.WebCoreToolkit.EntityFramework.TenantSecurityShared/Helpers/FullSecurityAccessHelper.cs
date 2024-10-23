using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers.Interfaces;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers
{
    public sealed class FullSecurityAccessHelper<TTrustConfig>:IDisposable where TTrustConfig : class, new()
    {
        public TTrustConfig DesiredTrust { get; set; }
        private readonly ITrustfulComponent<TTrustConfig> trustfulTarget;
        public FullSecurityAccessHelper<TTrustConfig> ForwardHelper { get; set; }

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

        public static FullSecurityAccessHelper<TTrustConfig> CreateForCaller<T>(ICoreSystemContext securityDb,
            T trustingObject, TTrustConfig desiredTrust = null) where T : ITrustfulComponent<TTrustConfig>
        {
            var stack = new StackTrace(new StackFrame(1, false));
            var type = stack.GetFrame(0).GetMethod().DeclaringType;
            var trustingType = trustingObject.GetType();
            if (type.Assembly == trustingType.Assembly)
            {
                return new FullSecurityAccessHelper<TTrustConfig>(trustingObject, desiredTrust??new TTrustConfig());
            }

            return CreateForCallerInternal(securityDb, trustingObject, desiredTrust);
        }

        private static FullSecurityAccessHelper<TTrustConfig> CreateForCallerInternal<T>(ICoreSystemContext securityDb, T trustingObject, TTrustConfig desiredTrust) where T: ITrustfulComponent<TTrustConfig>
        {
            var stack = new StackTrace(new StackFrame(2, false));
            var type = stack.GetFrame(0).GetMethod().DeclaringType;
            var trustingType = trustingObject.GetType();
            var cmp = securityDb.TrustedFullAccessComponents.FirstOrDefault(n =>
                n.FullQualifiedTypeName == type.AssemblyQualifiedName && n.TargetQualifiedTypeName == trustingType.AssemblyQualifiedName);
            if (cmp != null)
            {
                TTrustConfig trustConfig =
                    desiredTrust ?? JsonHelper.FromJsonString<TTrustConfig>(cmp.TrustLevelConfig);
                return new FullSecurityAccessHelper<TTrustConfig>(trustingObject, trustConfig);
            }

            throw new InvalidOperationException($"The caller ({type.AssemblyQualifiedName}) is not trusted for {trustingType.AssemblyQualifiedName}!");
        }

        public void Dispose()
        {
            trustfulTarget.RollbackSecurity(this);
        }
            
    }
}
