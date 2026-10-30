using System;
using ITVComponents.Helpers;
using ITVComponents.Logging;
using ITVComponents.Settings.Native;
using Microsoft.Extensions.Configuration;

namespace ITVComponents.EFRepo.Helpers
{
    public static class SchemaConfiguration
    {
        /// <summary>
        /// Resolves a configured schema-value. The value is either the schema itself or a reference to a configuration-path
        /// (<c>-&gt;Path:To:Schema</c> or <c>:--&gt;Path:To:Schema</c>, optionally with a default: <c>-&gt;Path??dbo</c>), so the
        /// schema can be maintained at a single location.
        /// </summary>
        /// <param name="configuredValue">the configured value</param>
        /// <param name="configuration">the configuration to resolve references against. When omitted, the NativeSettings-configuration is used</param>
        /// <returns>the resolved schema or null, when no schema is configured or a reference could not be resolved</returns>
        public static string Resolve(string configuredValue, IConfiguration configuration = null)
        {
            if (string.IsNullOrEmpty(configuredValue))
            {
                return null;
            }

            string path = null;
            if (configuredValue.StartsWith(":-->"))
            {
                path = configuredValue.Substring(4);
            }
            else if (configuredValue.StartsWith("->"))
            {
                path = configuredValue.Substring(2);
            }

            if (path == null)
            {
                return configuredValue;
            }

            string defaultValue = null;
            var defaultIndex = path.IndexOf("??", StringComparison.Ordinal);
            if (defaultIndex != -1)
            {
                defaultValue = path.Substring(defaultIndex + 2);
                path = path.Substring(0, defaultIndex);
            }

            string retVal = null;
            try
            {
                retVal = (configuration ?? NativeSettings.Configuration)[path];
            }
            catch (Exception ex)
            {
                LogEnvironment.LogEvent($"Unable to read the schema-reference '{path}' from the configuration: {ex.OutlineException()}", LogSeverity.Error);
            }

            if (string.IsNullOrEmpty(retVal) && !string.IsNullOrEmpty(defaultValue))
            {
                retVal = defaultValue;
            }

            if (string.IsNullOrEmpty(retVal))
            {
                LogEnvironment.LogEvent($"The schema-reference '{path}' did not resolve to a value. The default schema of the model is used.", LogSeverity.Warning);
                return null;
            }

            return retVal;
        }
    }
}
