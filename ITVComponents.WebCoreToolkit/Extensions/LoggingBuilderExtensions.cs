using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Logging;
using ITVComponents.WebCoreToolkit.Logging.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class LoggingBuilderExtensions
    {
        private static bool toolkitLoggingRegistered = false;
        /// <summary>
        /// Configures the Toolkit-Logger for a Web- or a service-application 
        /// </summary>
        /// <param name="builder">the used log-builder</param>
        /// <returns>the provided logbuilder object</returns>
        public static ILoggingBuilder UseToolkitLogging(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<IGlobalLogConfiguration, GlobalLogConfiguration>();
            builder.Services.AddSingleton<ILoggerProvider, ToolkitLogProvider>();
            builder.Services.AddSingleton(typeof(ILogger<>), typeof(CollectingLoggerDecorator<>));
            toolkitLoggingRegistered = true;
            return builder;
        }

        public static ILoggingBuilder UseToolkitConsoleLog(this ILoggingBuilder builder)
        {
            if (!toolkitLoggingRegistered)
            {
                builder.UseToolkitLogging();
            }

            builder.Services.AddSingleton<ILoggerProvider, ToolkitConsoleProvider>();
            return builder;
        }
    }
}
