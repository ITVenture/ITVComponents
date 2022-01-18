using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ITVComponents.WebCoreToolkit.Logging.Console
{
    internal class ToolkitConsoleLogger:ILogger
    {
        private readonly string categoryName;
        private readonly IGlobalLogConfiguration config;

        public ToolkitConsoleLogger(string categoryName, IGlobalLogConfiguration config)
        {
            this.categoryName = categoryName;
            this.config = config;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new ToolkitLogScope<TState>(this, state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return config.IsEnabled(logLevel, categoryName, true);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (config.IsEnabled(logLevel, categoryName))
            {
                System.Console.WriteLine($"{DateTime.Now:g} - [{eventId.Id,-7},{logLevel,-12}] - {categoryName} - {formatter(state,exception)}");
            }
        }
    }
}
