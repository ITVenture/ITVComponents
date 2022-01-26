using System;
using System.Collections.Generic;
using ITVComponents.WebCoreToolkit.Configuration;
using ITVComponents.WebCoreToolkit.EntityFramework.Options.Logging;
using ITVComponents.WebCoreToolkit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static void UpdateLoggingOptions(this IServiceProvider services)
        {
            IGlobalSettings<DbLoggingOptions> logSettings = services.GetService<IGlobalSettings<DbLoggingOptions>>();
            IGlobalLogConfiguration target = services.GetService<IGlobalLogConfiguration>();
            List<int> l = new List<int>();
            var opt = logSettings.Value;
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
}
