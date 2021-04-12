using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class LoggingBuilderExtensions
    {
        /// <summary>
        /// Configures the Toolkit-Logger for a Web- or a service-application 
        /// </summary>
        /// <param name="builder">the used log-builder</param>
        /// <returns>the provided logbuilder object</returns>
        public static ILoggingBuilder UseToolkitLogging(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, ToolkitLogProvider>();
            builder.Services.AddSingleton(typeof(ILogger<>), typeof(CollectingLoggerDecorator<>));
            return builder;
        }
    }
}
