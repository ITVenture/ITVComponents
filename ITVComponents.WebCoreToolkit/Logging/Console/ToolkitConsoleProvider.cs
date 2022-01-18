using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ITVComponents.WebCoreToolkit.Logging.Console
{
    internal class ToolkitConsoleProvider:ILoggerProvider
    {
        private readonly IGlobalLogConfiguration config;

        private ConcurrentDictionary<string, ToolkitConsoleLogger> availableLoggers = new ConcurrentDictionary<string, ToolkitConsoleLogger>();

        public ToolkitConsoleProvider(IGlobalLogConfiguration config)
        {
            this.config = config;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return availableLoggers.GetOrAdd(categoryName, s => new ToolkitConsoleLogger(s,config));
        }

        public void Dispose()
        {
            availableLoggers.Clear();
        }
    }
}
