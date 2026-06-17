using ITVComponents.Json;
using ITVComponents.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Helpers;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
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

        // Scoped (per circuit/request) cache of the trust-component table, keyed by (trustedTypeAQN, targetTypeAQN)
        // -> TrustLevelConfig JSON. The component set is global config that rarely changes; CreateForCaller is hit
        // dozens of times per render, and each previously ran a FirstOrDefault DB query against the *shared* scoped
        // DbContext. Serving from this cache removes those queries from the security hot path (and with them the
        // "a second operation was started on this context instance" crash when parallel Blazor lifecycle callbacks
        // hit the shared context concurrently). The one-time load runs on a dedicated, short-lived context instance
        // (never the shared one) so the load itself can't collide either.
        private ConcurrentDictionary<(string trusted, string target), string> trustConfigCache;
        private readonly object trustCacheLock = new();

        public DbSecurityAccessProvider(IServiceProvider services)
        {
            this.services = services;
        }

        private ICoreSystemContext SecurityDb => (securityDb ??= services.GetService<ICoreSystemContext>());

        private string ResolveTrustLevelConfig(string trustedTypeName, string trustingTypeName)
        {
            if (trustConfigCache == null)
            {
                lock (trustCacheLock)
                {
                    if (trustConfigCache == null)
                    {
                        var dict = new ConcurrentDictionary<(string, string), string>();
                        // Load on a fresh instance, never the shared scoped context. IgnoreQueryFilters keeps it
                        // tenant-agnostic (the trust table is global) and guarantees the load can't re-enter
                        // CurrentTenantId/scope resolution.
                        var loadCtx = (ICoreSystemContext)ActivatorUtilities.CreateInstance(services, SecurityDb.GetType());
                        using (loadCtx as IDisposable)
                        {
                            foreach (var c in loadCtx.TrustedFullAccessComponents.IgnoreQueryFilters().ToList())
                            {
                                dict[(c.FullQualifiedTypeName, c.TargetQualifiedTypeName)] = c.TrustLevelConfig;
                            }
                        }

                        trustConfigCache = dict;
                    }
                }
            }

            return trustConfigCache.TryGetValue((trustedTypeName, trustingTypeName), out var cfg) ? cfg : null;
        }

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
            // Trust lookup served from the scoped cache (loaded once on a dedicated context) instead of querying
            // the shared scoped DbContext on every call — see trustConfigCache.
            var trustLevelConfig = ResolveTrustLevelConfig(trustedType.AssemblyQualifiedName, trustingType.AssemblyQualifiedName);

            var configuredTrust = new TTrustConfig();
            if (trustLevelConfig != null)
            {
                configuredTrust =
                    JsonHelper.FromJsonString<TTrustConfig>(trustLevelConfig, SerializationTypingMode.StaticTyping);

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
