using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Security.ComponentTrust
{
    public class DbSecurityAccessProvider:ISecurityAccessProvider
    {
        private readonly IServiceProvider services;
        private ICoreSystemContext securityDb;

        public DbSecurityAccessProvider(IServiceProvider services)
        {
            this.services = services;
        }

        private ICoreSystemContext SecurityDb => (securityDb ??= services.GetService<ICoreSystemContext>());

        public IFullSecurityAccessHelper<TTrustConfig> CreateForCaller<TTrustConfig, T>(T trustingObject, TTrustConfig desiredTrust = null) where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            var stack = new StackTrace(new StackFrame(1, false));
            var type = stack.GetFrame(0).GetMethod().DeclaringType;
            var trustingType = trustingObject.GetType();
            if (type.Assembly == trustingType.Assembly)
            {
                return new FullSecurityAccessHelper<TTrustConfig>(trustingObject, desiredTrust ?? new TTrustConfig());
            }

            return CreateForCallerInternal(SecurityDb, trustingObject, trustingType, type, desiredTrust);
        }

        /*private IFullSecurityAccessHelper<TTrustConfig> CreateForCallerInternal<TTrustConfig, T>(IServiceProvider services,
            T trustingObject, Type trustingType, Type trustedType, TTrustConfig desiredTrust)
            where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            var csc = services.GetService<ICoreSystemContext>();
            return CreateForCallerInternal(csc, trustingObject, trustingType, trustedType, desiredTrust);
        }*/

        private FullSecurityAccessHelper<TTrustConfig> CreateForCallerInternal<TTrustConfig,T>(ICoreSystemContext securityDb, T trustingObject, Type trustingType, Type trustedType, TTrustConfig desiredTrust)
            where T : ITrustfulComponent<TTrustConfig> where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            var cmp = securityDb.TrustedFullAccessComponents.Local.FirstOrDefault(n =>
                n.FullQualifiedTypeName == trustedType.AssemblyQualifiedName && n.TargetQualifiedTypeName == trustingType.AssemblyQualifiedName);
            if (cmp == null)
            {
                cmp = securityDb.TrustedFullAccessComponents.FirstOrDefault(n =>
                    n.FullQualifiedTypeName == trustedType.AssemblyQualifiedName && n.TargetQualifiedTypeName == trustingType.AssemblyQualifiedName);
            }

            var configuredTrust = new TTrustConfig();
            if (cmp != null)
            {
                configuredTrust =
                    JsonHelper.FromJsonString<TTrustConfig>(cmp.TrustLevelConfig, SerializationTypingMode.StaticTyping);

            }
            else
            {
                LogEnvironment.LogEvent($"No Trust Configuration found for the caller ({trustedType.AssemblyQualifiedName}). No special permissions will be granted.", LogSeverity.Warning, "ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers.FullSecurityAccessHelper");
            }
            //throw new InvalidOperationException($"The caller ({type.AssemblyQualifiedName}) is not trusted for {trustingType.AssemblyQualifiedName}!");
            TTrustConfig trustConfig =
                desiredTrust ?? configuredTrust;
            return new FullSecurityAccessHelper<TTrustConfig>(trustingObject, trustConfig.ApplySpecialFilters(configuredTrust));
        }
    }
}
