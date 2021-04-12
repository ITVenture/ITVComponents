using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.DbLessConfig.Configurations;
using ITVComponents.WebCoreToolkit.Models;
using ITVComponents.WebCoreToolkit.WebPlugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITVComponents.WebCoreToolkit.DbLessConfig.WebPlugins
{
    internal class PluginsSelector:IWebPluginsSelector
    {
        private readonly IOptions<PluginsSettings> settings;
        private readonly ILogger<PluginsSelector> logger;

        public PluginsSelector(IOptions<PluginsSettings> settings, ILogger<PluginsSelector> logger)
        {
            this.settings = settings;
            this.logger = logger;
        }

        public IEnumerable<WebPlugin> GetStartupPlugins()
        {
            logger.Log(LogLevel.Debug, "Reading startup-plugins...");
            return from t in (settings.Value.WebPlugins??new WebPlugin[0]) where !string.IsNullOrEmpty(t.StartupRegistrationConstructor) select t;
        }

        public WebPlugin GetPlugin(string uniqueName)
        {
            logger.Log(LogLevel.Debug, $"Looking for Plugin {uniqueName}");
            return (from t in (settings.Value.WebPlugins ?? new WebPlugin[0]) where t.UniqueName == uniqueName && !string.IsNullOrEmpty(t.Constructor) select t).FirstOrDefault();
        }

        public IEnumerable<WebPlugin> GetAutoLoadPlugins()
        {
            return from t in (settings.Value.WebPlugins??new WebPlugin[0]) where t.AutoLoad && !string.IsNullOrEmpty(t.Constructor) select t;
        }

        public void ConfigurePlugin(WebPlugin pi)
        {
            logger?.Log(LogLevel.Debug, "Call to ConfigurePlugin is ignored for Settings-Plugins");
        }
    }
}
