using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Logging;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Helpers;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Security.ComponentTrust;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static void UpdateLoggingOptions(this IServiceProvider services)
        {
            IGlobalSettings<DbLoggingOptions> logSettings = services.GetService<IGlobalSettings<DbLoggingOptions>>();
            IGlobalLogConfiguration target = services.GetService<IGlobalLogConfiguration>();
            List<int> l = new List<int>();
            var opt = logSettings.ValueOrDefault;
            if (opt != null)
            {
                if (opt.LogEnabled)
                {
                    if (opt.LogCritical)
                    {
                        l.Add((int)LogLevel.Critical);
                    }

                    if (opt.LogDebug)
                    {
                        l.Add((int)LogLevel.Debug);
                    }

                    if (opt.LogError)
                    {
                        l.Add((int)LogLevel.Error);
                    }

                    if (opt.LogInformation)
                    {
                        l.Add((int)LogLevel.Information);
                    }

                    if (opt.LogTrace)
                    {
                        l.Add((int)LogLevel.Trace);
                    }

                    if (opt.LogWarning)
                    {
                        l.Add((int)LogLevel.Warning);
                    }

                    if (opt.LogNone)
                    {
                        l.Add((int)LogLevel.None);
                    }
                }

                var filters = new Dictionary<LogLevel, string[]>();
                if (opt.LogEnabled)
                {
                    if (opt.LogCritical && opt.CriticalFilters != null && opt.CriticalFilters.Length != 0)
                    {
                        filters.Add(LogLevel.Critical, opt.CriticalFilters);
                    }

                    if (opt.LogDebug && opt.DebugFilters != null && opt.DebugFilters.Length != 0)
                    {
                        filters.Add(LogLevel.Debug, opt.DebugFilters);
                    }

                    if (opt.LogError && opt.ErrorFilters != null && opt.ErrorFilters.Length != 0)
                    {
                        filters.Add(LogLevel.Error, opt.ErrorFilters);
                    }

                    if (opt.LogInformation && opt.InformationFilters != null && opt.InformationFilters.Length != 0)
                    {
                        filters.Add(LogLevel.Information, opt.InformationFilters);
                    }

                    if (opt.LogTrace && opt.TraceFilters != null && opt.TraceFilters.Length != 0)
                    {
                        filters.Add(LogLevel.Trace, opt.TraceFilters);
                    }

                    if (opt.LogWarning && opt.WarningFilters != null && opt.WarningFilters.Length != 0)
                    {
                        filters.Add(LogLevel.Warning, opt.WarningFilters);
                    }
                }

                target.Configure(l.ToArray(), filters);
            }
        }

        /*public static IFullSecurityAccessHelper TryGetTrust(this IServiceProvider services, object target,
            Type trustingType, Type trustedType, ITrustConfig desiredTrust)
        {
            var method = LambdaHelper.GetMethodInfo(() => TryGetTrust<DummyTrustConfig>(null, null, null, null, null))
                .GetGenericMethodDefinition();
            var genericMethod = method.MakeGenericMethod(desiredTrust.GetType());
            return (IFullSecurityAccessHelper)genericMethod.Invoke(null,
                new[] { services, target, trustingType, trustedType, desiredTrust });
        }

        public static IFullSecurityAccessHelper<TTrustConfig> TryGetTrust<TTrustConfig>(this IServiceProvider services,
            ITrustfulComponent<TTrustConfig> target, Type trustingType, Type trustedType, TTrustConfig desiredTrust)
            where TTrustConfig : class, ITrustConfig<TTrustConfig>, new()
        {
            return FullSecurityAccessHelper<TTrustConfig>.CreateForCallerInternal(services, target, trustingType,
                trustedType, desiredTrust);
        }

        private class DummyTrustConfig : ITrustConfig<DummyTrustConfig>
        {
            public DummyTrustConfig Clone()
            {
                return new DummyTrustConfig();
            }

            public IDictionary<string, bool> SpecialFilterSettings { get; set; }

            public DummyTrustConfig Clone(DummyTrustConfig other)
            {
                throw new NotImplementedException();
            }
        }*/
    }
}
