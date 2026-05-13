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
                System.Console.WriteLine($"{FormatLogLevel(logLevel),-12} - {DateTime.Now:g} - [{eventId.Id,-7}] - {categoryName} - {formatter(state,exception)}");
            }
        }

        private string FormatLogLevel(LogLevel logLevel)
        {
            //Console.WriteLine("\x1b[38;5;161m{0}\x1b[0m", "Holdrio!");
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return "\x1b[38;2;3;252;223mTRACE\x1b[0m";
                case LogLevel.Debug:
                    return "\x1b[38;2;165;3;252mDEBUG\x1b[0m";
                case LogLevel.Information:
                    return "\x1b[38;2;59;168;57mINFO\x1b[0m";
                case LogLevel.Warning:
                    return "\x1b[38;2;191;191;23mWARN\x1b[0m";
                case LogLevel.Error:
                    return "\x1b[38;2;252;3;3mERROR\x1b[0m";
                case LogLevel.Critical:
                    return "\x1b[38;2;252;3;59mCRITICAL\x1b[0m";
                case LogLevel.None:
                    return "NONE";
                default:
                    return "UNKNOWN";
            }
        }
    }
}
